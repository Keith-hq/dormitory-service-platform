using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Tests;

/// <summary>
/// 门岗登记仓储（VST-01/02/03）核心链路：
/// 登记（学号规范化与友好校验）→ 扫码核验（token 有效/过期/姓名一致）→ 离开记录。
/// </summary>
public class VisitorRegistryRepositoryTests
{
    private const string StudentId = "20260001";

    private static VisitorRegistryRepository CreateRepository(AppDbContext context)
        => new(context);

    private static CreateVisitorRegistryRequest CreateRequest(
        string visitorName = "张三",
        string? studentId = StudentId)
        => new() { VisitorName = visitorName, Phone = "13800000000", StudentId = studentId };

    // ===== VST-01 登记 =====

    [Fact]
    public async Task CreateAsync_ValidStudent_SetsPending()
    {
        await using var context = TestDbContextFactory.Create();
        context.Students.Add(new Student { StudentId = StudentId, Name = "王同学" });
        await context.SaveChangesAsync();

        var repo = CreateRepository(context);
        var result = await repo.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.RegistryId > 0);
        Assert.Equal("待核验", result.Status);
        Assert.Equal(StudentId, result.StudentId);
    }

    [Fact]
    public async Task CreateAsync_BlankStudentId_StoresNullLink()
    {
        await using var context = TestDbContextFactory.Create();
        var repo = CreateRepository(context);

        var result = await repo.CreateAsync(CreateRequest(studentId: "  "), CancellationToken.None);

        Assert.Null(result.StudentId); // 空串 → null，不关联 D_Student 外键
        Assert.Equal("待核验", result.Status);
    }

    [Fact]
    public async Task CreateAsync_InvalidStudentId_ThrowsFriendly400()
    {
        await using var context = TestDbContextFactory.Create();
        var repo = CreateRepository(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.CreateAsync(CreateRequest(studentId: "99999999"), CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("被访学生不存在", ex.Message);
    }

    // ===== VST-02 扫码核验 =====

    private static async Task<(AppDbContext Context, VisitorRegistry Registry)> SeedPendingRegistryAsync()
    {
        var context = TestDbContextFactory.Create();
        context.Students.Add(new Student { StudentId = StudentId, Name = "王同学" });
        context.VisitorAuthorizations.Add(new VisitorAuthorization
        {
            StudentId = StudentId,
            RoomId = 1,
            VisitorName = "张三",
            VisitReason = "探望",
            AuthorizationToken = "TOKEN-1",
            ExpiresTime = DateTime.Now.AddHours(2),
            Status = "有效",
            CreateTime = DateTime.Now
        });
        var registry = new VisitorRegistry
        {
            VisitorName = "张三",
            Phone = "13800000000",
            StudentId = StudentId,
            EnterTime = DateTime.Now,
            Status = "待核验"
            // 041：登记不再携带 CreateTime（与 EnterTime 同为 SYSDATE 的冗余列已删）
        };
        context.VisitorRegistries.Add(registry);
        await context.SaveChangesAsync();
        return (context, registry);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_MarksVerified()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        var repo = CreateRepository(context);

        var result = await repo.VerifyAsync(registry.RegistryId, "TOKEN-1", CancellationToken.None);

        Assert.Equal("已核验", result.Status);
        Assert.Equal("TOKEN-1", result.QrToken);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_Throws404()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        var repo = CreateRepository(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.VerifyAsync(registry.RegistryId, "NO-SUCH-TOKEN", CancellationToken.None));

        Assert.Equal(404, ex.HttpStatus);
        Assert.Contains("通行码无效", ex.Message);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredToken_Throws400()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        // 把授权改为已过期
        var auth = await context.VisitorAuthorizations.FirstAsync();
        auth.ExpiresTime = DateTime.Now.AddHours(-1);
        await context.SaveChangesAsync();

        var repo = CreateRepository(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.VerifyAsync(registry.RegistryId, "TOKEN-1", CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("已过期", ex.Message);
    }

    [Fact]
    public async Task VerifyAsync_WrongVisitorName_Throws400()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        // 登记访客姓名与通行码持有人不一致
        registry.VisitorName = "李四";
        await context.SaveChangesAsync();

        var repo = CreateRepository(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.VerifyAsync(registry.RegistryId, "TOKEN-1", CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("姓名不一致", ex.Message);
    }

    [Fact]
    public async Task VerifyAsync_AlreadyVerified_Throws400()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        registry.Status = "已核验";
        await context.SaveChangesAsync();

        var repo = CreateRepository(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.VerifyAsync(registry.RegistryId, "TOKEN-1", CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("不能重复操作", ex.Message);
    }

    // ===== VST-03 离开记录 =====

    [Fact]
    public async Task RecordExitAsync_Success_SetsLeftAndExitTime()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        var repo = CreateRepository(context);

        var result = await repo.RecordExitAsync(registry.RegistryId, CancellationToken.None);

        Assert.Equal("已离开", result.Status);
        Assert.NotNull(result.ExitTime);
    }

    [Fact]
    public async Task RecordExitAsync_AlreadyLeft_Throws400()
    {
        var (context, registry) = await SeedPendingRegistryAsync();
        registry.Status = "已离开";
        registry.ExitTime = DateTime.Now;
        await context.SaveChangesAsync();

        var repo = CreateRepository(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            repo.RecordExitAsync(registry.RegistryId, CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("已离开", ex.Message);
    }
}
