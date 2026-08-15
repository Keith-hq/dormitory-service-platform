using TemplateDormApi.Data;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 钱包服务层测试（难点②）：STU-06 低余额提醒（PR #58 P2 整改）与流水投影、
/// STU-04 学生账单查询（EF 联表投影 + 账期过滤，账期维度为账单 Year_Month）。
/// 缴费/充值走 SP（自 COMMIT），其业务逻辑由 Oracle 侧 test_sp_wallet.sql 覆盖。
/// </summary>
public class WalletServiceTests
{
    [Fact]
    public async Task GetWallet_LowBalance_ReturnsWarning()
    {
        var context = TestDbContextFactory.Create();
        var service = new WalletService(context, new FakeBillingService { Balance = 15m });

        var wallet = await service.GetWallet("S-LOW-001", "2026-07");

        Assert.Equal(15m, wallet.Balance);
        Assert.True(wallet.LowBalanceWarning);
        Assert.Equal(20m, wallet.LowBalanceThreshold);   // 服务端常量，待契约确认
    }

    [Fact]
    public async Task GetWallet_BalanceAtThreshold_NoWarning()
    {
        var context = TestDbContextFactory.Create();
        var service = new WalletService(context, new FakeBillingService { Balance = 20m });

        var wallet = await service.GetWallet("S-EDGE-001", "2026-07");

        Assert.False(wallet.LowBalanceWarning);
    }

    [Fact]
    public async Task GetWallet_NormalBalance_NoWarning()
    {
        var context = TestDbContextFactory.Create();
        var service = new WalletService(context, new FakeBillingService { Balance = 950m });

        var wallet = await service.GetWallet("S-OK-001", "2026-07");

        Assert.False(wallet.LowBalanceWarning);
        Assert.Equal(950m, wallet.Balance);
    }

    [Fact]
    public async Task GetWallet_ProjectsLogs()
    {
        var context = TestDbContextFactory.Create();
        var service = new WalletService(context, new FakeBillingService
        {
            Logs = new List<WalletLog>
            {
                new()
                {
                    LogId = 1, StudentId = "S001", Amount = 50m, TransactionType = "充值",
                    BeforeBalance = 0m, AfterBalance = 50m, IdempotencyKey = "K1"
                }
            }
        });

        var wallet = await service.GetWallet("S001", "2026-07");

        var log = Assert.Single(wallet.Logs);
        Assert.Equal(1, log.LogId);
        Assert.Equal("充值", log.TransactionType);
        Assert.Equal(50m, log.Amount);
    }

    // ==================== STU-04 学生账单查询 ====================

    [Fact]
    public async Task GetStudentFees_NoYearMonth_ReturnsOwnDetailsAcrossMonths()
    {
        var context = TestDbContextFactory.Create();
        await SeedFeesAsync(context);
        var service = new WalletService(context, new FakeBillingService());

        var fees = await service.GetStudentFees("S-F-001", null);

        Assert.Equal(2, fees.Count);
        Assert.Null(fees.YearMonth);
        // 按账期倒序：2026-07 在前
        Assert.Equal("2026-07", fees.Items[0].YearMonth);
        Assert.Equal("2026-06", fees.Items[1].YearMonth);
        // 他人生明细（S-F-999）不出现
        Assert.DoesNotContain(fees.Items, i => i.DetailId == 3);
        // total = 水 + 电
        Assert.Equal(90m, fees.Items[1].Total);
        Assert.Equal(1L, fees.Items[1].FeeId);
        Assert.Equal(101, fees.Items[1].RoomId);
        Assert.Equal("月度分摊", fees.Items[1].BillType);
        Assert.Equal("否", fees.Items[1].IsPaid);
    }

    [Fact]
    public async Task GetStudentFees_WithYearMonth_FiltersByBillMonth()
    {
        var context = TestDbContextFactory.Create();
        await SeedFeesAsync(context);
        var service = new WalletService(context, new FakeBillingService());

        var fees = await service.GetStudentFees("S-F-001", "2026-07");

        Assert.Equal("2026-07", fees.YearMonth);
        var item = Assert.Single(fees.Items);
        Assert.Equal(2, item.DetailId);
        Assert.Equal(2L, item.FeeId);
        Assert.Equal(50m, item.Total);   // 40 + 10
        Assert.Equal(31, item.StayDays);
        Assert.Equal(31, item.TotalDays);
        Assert.Equal("是", item.IsPaid);
    }

    [Fact]
    public async Task GetStudentFees_NoMatch_ReturnsEmptyItems()
    {
        var context = TestDbContextFactory.Create();
        await SeedFeesAsync(context);
        var service = new WalletService(context, new FakeBillingService());

        var fees = await service.GetStudentFees("S-F-001", "2026-08");

        Assert.Equal(0, fees.Count);
        Assert.Empty(fees.Items);
    }

    private static async Task SeedFeesAsync(AppDbContext context)
    {
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 101, YearMonth = "2026-06", WaterFee = 100m, PowerFee = 50m, PublishStatus = "已发布" },
            new UtilityFee { FeeId = 2, RoomId = 101, YearMonth = "2026-07", WaterFee = 80m, PowerFee = 20m, PublishStatus = "已发布" });
        context.FeeDetails.AddRange(
            new FeeDetail { DetailId = 1, FeeId = 1, StudentId = "S-F-001", RoomId = 101, WaterShare = 60m, PowerShare = 30m, StayDays = 30, TotalDays = 30, BillType = "月度分摊", IsPaid = "否" },
            new FeeDetail { DetailId = 2, FeeId = 2, StudentId = "S-F-001", RoomId = 101, WaterShare = 40m, PowerShare = 10m, StayDays = 31, TotalDays = 31, BillType = "月度分摊", IsPaid = "是" },
            new FeeDetail { DetailId = 3, FeeId = 1, StudentId = "S-F-999", RoomId = 101, WaterShare = 40m, PowerShare = 20m, StayDays = 30, TotalDays = 30, BillType = "月度分摊", IsPaid = "否" });
        await context.SaveChangesAsync();
    }

    private sealed class FakeBillingService : IBillingService
    {
        public decimal Balance { get; set; }
        public List<WalletLog> Logs { get; set; } = new();

        public Task AutoDeduct(int attemptNo, string yearMonth) => Task.CompletedTask;
        public Task CheckPowerCut(string yearMonth) => Task.CompletedTask;
        public Task RestorePower() => Task.CompletedTask;
        public Task<decimal> GetBalance(string studentId) => Task.FromResult(Balance);
        public Task<List<WalletLog>> GetWalletLogs(string studentId, string yearMonth)
            => Task.FromResult(Logs);
        public Task<string> GetPowerStatus(int roomId) => Task.FromResult("正常");
    }
}
