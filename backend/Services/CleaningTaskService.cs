using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 保洁任务实现（DORM-39/40）。
/// 完成（DORM-40）状态机 待处理→已完成，已完成的重复完成幂等返回成功
/// （满足 IT-C9-002 复跑同值）。
/// </summary>
public sealed class CleaningTaskService : ICleaningTaskService
{
    private readonly AppDbContext _context;

    public CleaningTaskService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<CleaningTaskDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.CleaningTasks.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.TaskId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_context.Facilities.AsNoTracking(),
                task => task.FacilityId,
                facility => facility.FacilityId,
                (task, facility) => new CleaningTaskDto
                {
                    TaskId = task.TaskId,
                    FacilityId = task.FacilityId,
                    FacilityCode = facility.FacilityCode,
                    TriggerCount = task.TriggerCount,
                    Status = task.Status,
                    CreateTime = task.CreateTime,
                    CompleteTime = task.CompleteTime
                })
            .ToListAsync(cancellationToken);

        return new PagedResult<CleaningTaskDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CleaningTaskDto> CompleteAsync(int taskId, CancellationToken cancellationToken)
    {
        var task = await _context.CleaningTasks
            .FirstOrDefaultAsync(item => item.TaskId == taskId, cancellationToken)
            ?? throw new BusinessException(404, "保洁任务不存在", StatusCodes.Status404NotFound);

        if (task.Status == "已完成")
        {
            // 幂等：重复完成直接返回成功，不重复写 Complete_Time。
            return ToDto(task);
        }

        if (task.Status != "待处理")
        {
            throw new BusinessException(409, "只有「待处理」状态的保洁任务才能完成", StatusCodes.Status409Conflict);
        }

        task.Status = "已完成";
        task.CompleteTime = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    private static CleaningTaskDto ToDto(CleaningTask item) => new()
    {
        TaskId = item.TaskId,
        FacilityId = item.FacilityId,
        TriggerCount = item.TriggerCount,
        Status = item.Status,
        CreateTime = item.CreateTime,
        CompleteTime = item.CompleteTime
    };
}
