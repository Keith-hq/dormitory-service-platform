using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 住宿分配服务（DORM-08 入住 / DORM-09 调寝 / DORM-10 住户查询）。
/// 刘润东为 D_Allocation/床位唯一数据拥有者；床位并发唯一由 UK_D_BED_ALLOC_ACTIVE 兜底，
/// 调寝并发唯一由 CheckOut_Date 并发令牌兜底。
/// </summary>
public class AllocationService : IAllocationService
{
    private const string PkConstraint = "PK_D_BED_ALLOCATION";
    private const string UkActiveConstraint = "UK_D_BED_ALLOC_ACTIVE";

    private readonly AppDbContext _context;
    private readonly BedAllocationRepository _allocRepo;

    public AllocationService(AppDbContext context, BedAllocationRepository allocRepo)
    {
        _context = context;
        _allocRepo = allocRepo;
    }

    public async Task<BedAllocation> CreateAsync(AllocationCreateDto dto, int? currentAccountId = null)
    {
        var studentId = await ResolveStudentIdAsync(dto.StudentId, currentAccountId);

        var room = await _context.Rooms.FindAsync(dto.RoomId)
            ?? throw new BusinessException(404, "房间不存在", 404);

        if (!await _context.Students.AnyAsync(s => s.StudentId == studentId))
            throw new BusinessException(404, "学生不存在", 404);

        if (room.Capacity.HasValue && (room.Occupancy ?? 0) >= room.Capacity.Value)
            throw new BusinessException(400, "房间已满，无可用床位");

        if (await _allocRepo.GetActiveByStudentAsync(studentId) != null)
            throw new BusinessException(409, "该学生已有在住床位", 409);

        if (await _allocRepo.GetActiveByRoomBedAsync(dto.RoomId, dto.BedNo) != null)
            throw new BusinessException(409, "床位已占用", 409);

        try
        {
            return await DbSaveRetry.InsertWithPkRetryAsync(
                _context, PkConstraint, _allocRepo.NextAllocationIdAsync,
                async id =>
                {
                    // 重试时实体已随 ChangeTracker.Clear() 脱离跟踪，须在委托内重取
                    var currentRoom = await _context.Rooms.FindAsync(dto.RoomId)
                        ?? throw new BusinessException(404, "房间不存在", 404);
                    currentRoom.Occupancy = (currentRoom.Occupancy ?? 0) + 1;

                    var alloc = new BedAllocation
                    {
                        AllocationId = id,
                        StudentId = studentId,
                        RoomId = dto.RoomId,
                        BedNo = dto.BedNo,
                        CheckInDate = dto.CheckInDate
                    };
                    await _allocRepo.AddAsync(alloc);
                    return alloc;
                });
        }
        catch (DbUpdateException ex)
            when (DbSaveRetry.TryGetUniqueConstraintName(ex) == UkActiveConstraint)
        {
            // 并发抢占同一床位：数据库唯一索引兜底（IT-C2-002 ① 仅一人成功）
            throw new BusinessException(409, "床位已占用", 409);
        }
    }

    public async Task<BedAllocation> TransferAsync(int allocationId, AllocationTransferDto dto)
    {
        for (var attempt = 1; attempt <= DbSaveRetry.MaxAttempts; attempt++)
        {
            // 每次尝试取最新数据（重试时前次改动已被 ChangeTracker.Clear() 丢弃）
            var current = await _allocRepo.GetByIdAsync(allocationId)
                ?? throw new BusinessException(404, "住宿分配不存在", 404);

            if (current.CheckOutDate != null)
                throw new BusinessException(409, "该住宿分配已结束（已退宿/已调寝）", 409);

            if (!current.RoomId.HasValue)
                throw new BusinessException(400, "住宿分配缺少房间信息");

            var targetRoom = await _context.Rooms.FindAsync(dto.TargetRoomId)
                ?? throw new BusinessException(404, "目标房间不存在", 404);

            if (await _allocRepo.GetActiveByRoomBedAsync(dto.TargetRoomId, dto.TargetBedNo) != null)
                throw new BusinessException(409, "目标床位已占用", 409);

            var sameRoom = current.RoomId.Value == dto.TargetRoomId;
            Room? oldRoom = null;
            if (!sameRoom)
            {
                // Room 主键为 int，Find 需精确类型（BedAllocation.RoomId 为 long）
                oldRoom = await _context.Rooms.FindAsync((int)current.RoomId.Value)
                    ?? throw new BusinessException(404, "原房间不存在", 404);
                if ((oldRoom.Occupancy ?? 0) <= 0)
                    throw new BusinessException(400, "原房间占用数异常，无法调寝");
            }

            var now = DateTime.Now;
            current.CheckOutDate = now; // 并发令牌：并发调寝同一分配仅一个事务能写入成功

            var moved = new BedAllocation
            {
                AllocationId = await _allocRepo.NextAllocationIdAsync(),
                StudentId = current.StudentId,
                RoomId = dto.TargetRoomId,
                BedNo = dto.TargetBedNo,
                CheckInDate = now
            };
            await _context.BedAllocations.AddAsync(moved);

            if (!sameRoom)
            {
                // 旧房-1 新房+1，与新旧分配同一 SaveChanges 事务（IT-C2-006 ① 任一步失败全回滚）
                oldRoom!.Occupancy = (oldRoom.Occupancy ?? 1) - 1;
                targetRoom.Occupancy = (targetRoom.Occupancy ?? 0) + 1;
            }

            try
            {
                await _context.SaveChangesAsync();
                return moved;
            }
            catch (DbUpdateConcurrencyException)
            {
                // 并发调寝竞争失败：重读后判定（IT-C2-006 ③ 同一学生仅一次生效）
                // 注意：必须先于 DbUpdateException 捕获（其为 DbUpdateException 子类）
                _context.ChangeTracker.Clear();
                continue;
            }
            catch (DbUpdateException ex)
            {
                var constraint = DbSaveRetry.TryGetUniqueConstraintName(ex);
                if (constraint == UkActiveConstraint)
                    throw new BusinessException(409, "目标床位已占用", 409);
                if (constraint == PkConstraint)
                {
                    _context.ChangeTracker.Clear(); // 主键撞号，重取 MAX 重试
                    continue;
                }
                throw;
            }
        }

        throw new BusinessException(500, "调寝失败：并发冲突重试超限，请稍后重试", 500);
    }

    public async Task<object> GetOccupantsAsync(int roomId)
    {
        var room = await _context.Rooms.FindAsync(roomId)
            ?? throw new BusinessException(404, "房间不存在", 404);

        var allocations = await _allocRepo.GetActiveByRoomAsync(roomId);
        var studentIds = allocations
            .Select(a => a.StudentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var students = await _context.Students
            .Where(s => studentIds.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId);

        var items = allocations.Select(a => new
        {
            studentId = a.StudentId,
            studentName = a.StudentId != null && students.TryGetValue(a.StudentId, out var stu)
                ? stu.Name
                : null,
            bedNo = a.BedNo,
            checkInDate = a.CheckInDate
        }).ToList();

        return new { roomId, occupancy = room.Occupancy, items };
    }

    /// <summary>studentId 契约字段缺失时（IT-C2-002 并发用例）从登录态账户解析</summary>
    private async Task<string> ResolveStudentIdAsync(string? bodyStudentId, int? currentAccountId)
    {
        if (!string.IsNullOrWhiteSpace(bodyStudentId))
            return bodyStudentId;

        if (currentAccountId.HasValue)
        {
            var accountStudentId = await _context.UserAccounts
                .Where(a => a.AccountId == currentAccountId.Value)
                .Select(a => a.StudentId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(accountStudentId))
                return accountStudentId;
        }

        throw new BusinessException(400, "缺少学号：请求体未带 studentId 且无法从登录态解析");
    }
}
