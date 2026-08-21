using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 快递模块：确认取件写入 Pickup_Time、重复取件被拒、越权被拒。
/// </summary>
public class ParcelServiceTests
{
    private static (AppDbContext Write, AppDbContext Read) CreateContexts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"parcel-tests-{Guid.NewGuid():N}")
            .Options;
        return (new AppDbContext(options), new AppDbContext(options));
    }

    [Fact]
    public async Task PickupAsync_WritesPickupTime()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;

        context.ParcelRecords.Add(new ParcelRecord
        {
            StudentId = "S001",
            ArriveTime = DateTime.Now,
            CourierCompany = "顺丰"
        });
        await context.SaveChangesAsync();
        var parcelId = context.ParcelRecords.First().ParcelId;

        var service = new ParcelService(new ParcelRepository(context));
        var parcel = await service.PickupAsync(parcelId, "S001");

        Assert.NotNull(parcel.PickupTime);

        var persisted = await readContext.ParcelRecords.FindAsync(parcelId);
        Assert.NotNull(persisted!.PickupTime);
    }

    [Fact]
    public async Task PickupAsync_AlreadyPickedUp_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        context.ParcelRecords.Add(new ParcelRecord
        {
            StudentId = "S001",
            ArriveTime = DateTime.Now,
            PickupTime = DateTime.Now
        });
        await context.SaveChangesAsync();
        var parcelId = context.ParcelRecords.First().ParcelId;

        var service = new ParcelService(new ParcelRepository(context));
        await Assert.ThrowsAsync<BusinessException>(() => service.PickupAsync(parcelId, "S001"));
    }

    [Fact]
    public async Task PickupAsync_OtherStudent_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        context.ParcelRecords.Add(new ParcelRecord
        {
            StudentId = "S001",
            ArriveTime = DateTime.Now
        });
        await context.SaveChangesAsync();
        var parcelId = context.ParcelRecords.First().ParcelId;

        var service = new ParcelService(new ParcelRepository(context));
        await Assert.ThrowsAsync<BusinessException>(() => service.PickupAsync(parcelId, "S002"));
    }
}
