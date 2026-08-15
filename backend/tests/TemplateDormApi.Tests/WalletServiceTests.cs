using TemplateDormApi.Data;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 钱包服务层测试（难点②）：STU-06 低余额提醒（PR #58 P2 整改）与流水投影。
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
