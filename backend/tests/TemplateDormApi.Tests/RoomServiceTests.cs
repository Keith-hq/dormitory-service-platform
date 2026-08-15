using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// Room 模块：迁移 016 加列后 Floor/Status 的落库链路（评审 P3 补链回归）。
/// 覆盖：创建时 floor 落库、status 默认"正常"、DORM-07 修改 status 真正持久化。
/// 写入与读取使用共享内存库名、不同 context：读取 context 无跟踪缓存，必读 store，
/// 可验证"落库"（若属性 NotMapped 未持久化，读取 context 将得到旧值/空）。
/// </summary>
public class RoomServiceTests
{
    private static (AppDbContext Write, AppDbContext Read) CreateContexts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"room-tests-{Guid.NewGuid():N}")
            .Options;
        return (new AppDbContext(options), new AppDbContext(options));
    }

    [Fact]
    public async Task CreateAsync_PersistsFloorAndDefaultsStatusToNormal()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;
        var service = new RoomService(new RoomRepository(context));

        var created = await service.CreateAsync(new RoomCreateDto
        {
            BuildingId = 1,
            RoomNo = "101",
            Floor = 3,
            Capacity = 4
        });

        // 从独立 context 重新查询，验证写入数据库的值（而非内存对象）
        var room = await readContext.Rooms.FindAsync(created.RoomId);

        Assert.NotNull(room);
        Assert.Equal(3, room!.Floor);
        Assert.Equal("正常", room.Status);
    }

    [Fact]
    public async Task UpdateAsync_StatusChangePersistsToDatabase()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;
        var service = new RoomService(new RoomRepository(context));

        var created = await service.CreateAsync(new RoomCreateDto
        {
            BuildingId = 1,
            RoomNo = "102",
            Floor = 2,
            Capacity = 4
        });

        await service.UpdateAsync(created.RoomId, new RoomUpdateDto { Status = "停用" });

        // 从独立 context 重新查询，验证 status 修改真正落库（回归：曾因 NotMapped 静默失效）
        var room = await readContext.Rooms.FindAsync(created.RoomId);

        Assert.NotNull(room);
        Assert.Equal("停用", room!.Status);
    }

    /// <summary>
    /// DORM-06 UK 冲突测试替身：arming 后的首次 SaveChanges 先落一个"并发批次抢先建"的同号房间，
    /// 再抛同构异常模拟真库 ORA-00001 命中 UK_D_ROOM_BUILDING_NO（InMemory 不强制唯一索引，
    /// 无法真撞索引，只能同构模拟；OracleConstraintParser 兼容消息前缀解析）。
    /// </summary>
    private sealed class UkConflictOnceDbContext : AppDbContext
    {
        private readonly Room _concurrentWinner;
        private bool _armed;
        private bool _thrown;

        public UkConflictOnceDbContext(DbContextOptions<AppDbContext> options, Room concurrentWinner)
            : base(options) => _concurrentWinner = concurrentWinner;

        public void ArmUkConflict() => _armed = true;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_armed && !_thrown)
            {
                _thrown = true;
                Rooms.Add(_concurrentWinner);
                // 本批次房间先脱离跟踪：只落"并发批次抢先建"的房间，再报唯一索引冲突
                foreach (var entry in ChangeTracker.Entries<Room>()
                             .Where(e => !ReferenceEquals(e.Entity, _concurrentWinner)).ToList())
                {
                    entry.State = EntityState.Detached;
                }
                base.SaveChangesAsync(cancellationToken).GetAwaiter().GetResult();
                throw new DbUpdateException("模拟唯一索引冲突",
                    new Exception("ORA-00001: unique constraint (DORM_OPER.UK_D_ROOM_BUILDING_NO) violated"));
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task BatchInit_ConcurrentUkConflict_SkipsExistingAndDoesNotThrow()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"room-uk-tests-{Guid.NewGuid():N}")
            .Options;
        await using var context = new UkConflictOnceDbContext(options, new Room
        {
            BuildingId = 1,
            RoomNumber = "103",
            Floor = 1,
            Capacity = 4,
            Occupancy = 0,
            Status = "正常",
            PowerStatus = "正常"
        });
        context.Buildings.Add(new Building
        {
            BuildingId = 1,
            BuildingName = "一号楼",
            BuildingType = "男生宿舍",
            FloorCount = 6
        });
        await context.SaveChangesAsync();
        context.ArmUkConflict(); // 之后首次 SaveChanges：并发批次抢先建 103 → UK_D_ROOM_BUILDING_NO 冲突

        var service = new RoomService(new RoomRepository(context));
        var result = await service.BatchInitAsync(new RoomBatchInitDto
        {
            BuildingId = 1,
            Floor = 1,
            StartRoomNo = "101",
            Count = 3,
            Capacity = 4
        }, $"uk-conflict-key-{Guid.NewGuid():N}");

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("\"skipped\":[\"103\"]", json); // 冲突号归入"已存在跳过"，不 500
        Assert.Contains("\"total\":2", json);           // 101/102 仍创建成功

        // 落库校验：101/102/103 各一行（103 来自并发批次，本批次不重复插入）
        var roomNumbers = await context.Rooms
            .Where(r => r.BuildingId == 1 && r.Floor == 1)
            .Select(r => r.RoomNumber)
            .OrderBy(r => r)
            .ToListAsync();
        Assert.Equal(new[] { "101", "102", "103" }, roomNumbers);
    }

    [Fact]
    public async Task BatchInit_SameKeyConcurrentRequests_BothCompleteWithoutError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"room-cc-tests-{Guid.NewGuid():N}")
            .Options;
        // 两个独立 context 共享同一内存库：并发请求用各自 context 执行；
        // 进程内幂等缓存为 static，跨 context 生效（同键重放返回首次结果）。
        await using var context1 = new AppDbContext(options);
        await using var context2 = new AppDbContext(options);
        context1.Buildings.Add(new Building
        {
            BuildingId = 1,
            BuildingName = "一号楼",
            BuildingType = "男生宿舍",
            FloorCount = 6
        });
        await context1.SaveChangesAsync();

        var service1 = new RoomService(new RoomRepository(context1));
        var service2 = new RoomService(new RoomRepository(context2));
        var dto = new RoomBatchInitDto
        {
            BuildingId = 1,
            Floor = 1,
            StartRoomNo = "201",
            Count = 3,
            Capacity = 4
        };
        var key = $"concurrent-key-{Guid.NewGuid():N}";

        // 同键并发：任一执行顺序都不应抛错（真库由 UK_D_ROOM_BUILDING_NO 兜底防重；
        // InMemory 不强制唯一索引，此测试验证"不 500"与缓存一致）
        var results = await Task.WhenAll(
            service1.BatchInitAsync(dto, key),
            service2.BatchInitAsync(dto, key));
        Assert.All(results, r => Assert.NotNull(r));

        // 后续同键重放：命中缓存，replayed=true
        var replay = await service1.BatchInitAsync(dto, key);
        var json = JsonSerializer.Serialize(replay, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("\"replayed\":true", json);
    }
}
