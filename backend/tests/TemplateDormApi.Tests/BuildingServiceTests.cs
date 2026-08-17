using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 楼栋模块删除链路（IT-C10-002 ②）：被引用楼栋删除应返回明确业务错误而非 500。
/// 覆盖：有房间业务预检、无关联正常删除、不存在返回 false、数据库外键冲突兜底（同构 ORA-02292 模拟）。
/// </summary>
public class BuildingServiceTests
{
    private static DbContextOptions<AppDbContext> CreateOptions(string name)
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid():N}")
            .Options;

    private static Building NewBuilding(int id = 1) => new()
    {
        BuildingId = id,
        BuildingName = "一号楼",
        BuildingType = "男生宿舍",
        FloorCount = 6
    };

    private static Room NewRoom(string roomNumber, int buildingId = 1) => new()
    {
        BuildingId = buildingId,
        RoomNumber = roomNumber,
        Floor = 1,
        Capacity = 4,
        Occupancy = 0,
        Status = "正常",
        PowerStatus = "正常"
    };

    [Fact]
    public async Task DeleteAsync_WithRooms_ThrowsBusinessError()
    {
        var options = CreateOptions("building-rooms");
        await using var context = new AppDbContext(options);
        await using var readContext = new AppDbContext(options); // 独立跟踪，必读 store
        context.Buildings.Add(NewBuilding());
        context.Rooms.Add(NewRoom("101"));
        await context.SaveChangesAsync();

        var service = new BuildingService(new BuildingRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(1));

        Assert.Equal(400, ex.Code);
        Assert.Contains("房间", ex.Message);
        Assert.NotNull(await readContext.Buildings.FindAsync(1)); // 业务预检拦截，楼栋未被删除
    }

    [Fact]
    public async Task DeleteAsync_WithoutRooms_Succeeds()
    {
        var options = CreateOptions("building-ok");
        await using var context = new AppDbContext(options);
        context.Buildings.Add(NewBuilding());
        await context.SaveChangesAsync();

        var service = new BuildingService(new BuildingRepository(context));

        Assert.True(await service.DeleteAsync(1));
        Assert.Null(await context.Buildings.FindAsync(1));
    }

    [Fact]
    public async Task DeleteAsync_NotExists_ReturnsFalse()
    {
        var options = CreateOptions("building-missing");
        await using var context = new AppDbContext(options);
        var service = new BuildingService(new BuildingRepository(context));

        Assert.False(await service.DeleteAsync(999));
    }

    /// <summary>
    /// ORA-02292 外键冲突替身：InMemory 不建外键，删除被引用楼栋不会真撞 ORA-02292，
    /// 用同构消息模拟（OracleConstraintParser 兼容 Number=2292 / 消息前缀判断）。
    /// 注意种子数据用普通 context 写入同一内存库后再换本替身执行删除。
    /// </summary>
    private sealed class FkViolationDbContext : AppDbContext
    {
        public FkViolationDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("模拟外键冲突",
                new Exception("ORA-02292: integrity constraint (DORM_OPER.FK_ROOM_BUILDING) violated - child record found"));
    }

    [Fact]
    public async Task DeleteAsync_DbForeignKeyViolation_FallsBackToBusinessError()
    {
        var options = CreateOptions("building-fk");
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Buildings.Add(NewBuilding());
            await seedContext.SaveChangesAsync();
        }

        await using var context = new FkViolationDbContext(options); // 业务预检通过，删除落库时抛 ORA-02292
        await using var readContext = new AppDbContext(options);
        var service = new BuildingService(new BuildingRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(1));

        Assert.Equal(400, ex.Code);
        Assert.Contains("无法删除", ex.Message);
        Assert.NotNull(await readContext.Buildings.FindAsync(1)); // 兜底路径：楼栋未被删除
    }
}
