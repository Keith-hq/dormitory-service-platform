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
}
