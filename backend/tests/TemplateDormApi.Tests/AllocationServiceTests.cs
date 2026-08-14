using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 住宿分配模块：DORM-08 入住（占用+1/床位唯一/一人一床）、DORM-09 调寝（旧房-1 新房+1 原子）、
/// DORM-10 住户查询（联学生姓名）。并发令牌与唯一索引路径依赖 Oracle，由 8/14 集成测试覆盖。
/// </summary>
public class AllocationServiceTests
{
    private static (AppDbContext Context, AllocationService Service) CreateService(
        Action<AppDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"alloc-tests-{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        seed?.Invoke(context);
        context.SaveChanges();
        return (context, new AllocationService(context, new BedAllocationRepository(context)));
    }

    private static Room MakeRoom(int id, int capacity, int occupancy = 0) => new()
    {
        RoomId = id,
        BuildingId = 1,
        RoomNumber = id.ToString(),
        Capacity = capacity,
        Occupancy = occupancy,
        Status = "正常",
        PowerStatus = "正常"
    };

    private static void SeedBasic(AppDbContext context)
    {
        context.Rooms.AddRange(MakeRoom(101, 4), MakeRoom(201, 4));
        context.Students.Add(new Student { StudentId = "S001", Name = "张三" });
        context.Students.Add(new Student { StudentId = "S002", Name = "李四" });
    }

    private static AllocationCreateDto ValidDto(string studentId = "S001") => new()
    {
        StudentId = studentId,
        RoomId = 101,
        BedNo = 1,
        CheckInDate = new DateTime(2026, 8, 1)
    };

    [Fact]
    public async Task Create_PersistsAllocationAndIncrementsOccupancy()
    {
        var (context, service) = CreateService(SeedBasic);
        await using var _ = context;

        var alloc = await service.CreateAsync(ValidDto());

        Assert.True(alloc.AllocationId > 0);
        Assert.Equal(101, alloc.RoomId);
        var room = await context.Rooms.FindAsync(101);
        Assert.Equal(1, room!.Occupancy);
    }

    [Fact]
    public async Task Create_DuplicateBed_ThrowsOccupied()
    {
        var (context, service) = CreateService(SeedBasic);
        await using var _ = context;

        await service.CreateAsync(ValidDto("S001"));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.CreateAsync(ValidDto("S002")));
        Assert.Contains("床位已占用", ex.Message);

        // 失败不残留占用数变化
        var room = await context.Rooms.FindAsync(101);
        Assert.Equal(1, room!.Occupancy);
    }

    [Fact]
    public async Task Create_DuplicateActiveStudent_Throws()
    {
        var (context, service) = CreateService(SeedBasic);
        await using var _ = context;

        await service.CreateAsync(ValidDto("S001"));

        var dto = ValidDto("S001");
        dto.RoomId = 201;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(dto));
        Assert.Contains("已有在住床位", ex.Message);
    }

    [Fact]
    public async Task Create_RoomFull_Throws()
    {
        var (context, service) = CreateService(c =>
        {
            c.Rooms.Add(MakeRoom(101, 1, 1));
            c.Students.Add(new Student { StudentId = "S001", Name = "张三" });
        });
        await using var _ = context;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(ValidDto()));
        Assert.Contains("房间已满", ex.Message);
    }

    [Fact]
    public async Task Transfer_MovesBedAndRebalancesOccupancy()
    {
        var (context, service) = CreateService(c =>
        {
            c.Rooms.Add(MakeRoom(101, 4, 2));
            c.Rooms.Add(MakeRoom(201, 4, 0));
            c.Students.Add(new Student { StudentId = "S001", Name = "张三" });
            c.BedAllocations.Add(new BedAllocation
            {
                AllocationId = 1,
                StudentId = "S001",
                RoomId = 101,
                BedNo = 1,
                CheckInDate = new DateTime(2026, 8, 1)
            });
        });
        await using var _ = context;

        var moved = await service.TransferAsync(1, new AllocationTransferDto { TargetRoomId = 201, TargetBedNo = 1 });

        Assert.Equal(201, moved.RoomId);
        Assert.Equal(1, moved.BedNo);

        var old = await context.BedAllocations.FindAsync(1L);
        Assert.NotNull(old!.CheckOutDate); // 旧分配关闭

        var oldRoom = await context.Rooms.FindAsync(101);
        var newRoom = await context.Rooms.FindAsync(201);
        Assert.Equal(1, oldRoom!.Occupancy); // 旧房-1
        Assert.Equal(1, newRoom!.Occupancy); // 新房+1
    }

    [Fact]
    public async Task Transfer_TargetBedTaken_Throws()
    {
        var (context, service) = CreateService(c =>
        {
            c.Rooms.Add(MakeRoom(101, 4, 2));
            c.Rooms.Add(MakeRoom(201, 4, 1));
            c.Students.Add(new Student { StudentId = "S001", Name = "张三" });
            c.Students.Add(new Student { StudentId = "S002", Name = "李四" });
            c.BedAllocations.AddRange(
                new BedAllocation { AllocationId = 1, StudentId = "S001", RoomId = 101, BedNo = 1, CheckInDate = new DateTime(2026, 8, 1) },
                new BedAllocation { AllocationId = 2, StudentId = "S002", RoomId = 201, BedNo = 1, CheckInDate = new DateTime(2026, 8, 1) });
        });
        await using var _ = context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.TransferAsync(1, new AllocationTransferDto { TargetRoomId = 201, TargetBedNo = 1 }));
        Assert.Contains("目标床位已占用", ex.Message);
    }

    [Fact]
    public async Task Transfer_ClosedAllocation_Throws()
    {
        var (context, service) = CreateService(c =>
        {
            c.Rooms.Add(MakeRoom(101, 4, 0));
            c.Rooms.Add(MakeRoom(201, 4, 0));
            c.Students.Add(new Student { StudentId = "S001", Name = "张三" });
            c.BedAllocations.Add(new BedAllocation
            {
                AllocationId = 1,
                StudentId = "S001",
                RoomId = 101,
                BedNo = 1,
                CheckInDate = new DateTime(2026, 8, 1),
                CheckOutDate = new DateTime(2026, 8, 10)
            });
        });
        await using var _ = context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => service.TransferAsync(1, new AllocationTransferDto { TargetRoomId = 201, TargetBedNo = 1 }));
        Assert.Contains("已结束", ex.Message);
    }

    [Fact]
    public async Task Occupants_JoinsStudentNames()
    {
        var (context, service) = CreateService(c =>
        {
            c.Rooms.Add(MakeRoom(101, 4, 1));
            c.Students.Add(new Student { StudentId = "S001", Name = "张三" });
            c.BedAllocations.Add(new BedAllocation
            {
                AllocationId = 1,
                StudentId = "S001",
                RoomId = 101,
                BedNo = 2,
                CheckInDate = new DateTime(2026, 8, 1)
            });
            // 已退宿的不应出现在住户列表
            c.BedAllocations.Add(new BedAllocation
            {
                AllocationId = 2,
                StudentId = "S001",
                RoomId = 101,
                BedNo = 3,
                CheckInDate = new DateTime(2026, 7, 1),
                CheckOutDate = new DateTime(2026, 7, 31)
            });
        });
        await using var _ = context;

        var result = await service.GetOccupantsAsync(101);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Assert.Contains("张三", json);
        Assert.Contains("\"bedNo\":2", json);
        Assert.DoesNotContain("\"bedNo\":3", json);
    }
}
