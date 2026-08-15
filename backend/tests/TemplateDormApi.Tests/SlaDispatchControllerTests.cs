using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TemplateDormApi.Controllers;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// SLA 派单控制器测试（难点⑤ 三审）：
/// 1. DORM-26~28 授权模型——RepairStaff 策略（维修员/楼长/超级管理员）真实求值：允许/403 路径；
/// 2. 契约路由 SVC-SCHED-03 = POST /api/internal/scheduler/sla-escalation；
/// 3. DORM-28 repairResult 字段 + 枚举校验（已修复/需更换配件/无法修复）；
/// 4. 控制器成功路径（JWT 身份解析 + 分页返回 + 接单/完工）。
/// </summary>
public class SlaDispatchControllerTests
{
    // ==================== 授权策略求值（真实 Policy 求值，等价 403/放行） ====================

    [Theory]
    [InlineData("repairman", true)]   // 维修员
    [InlineData("admin", true)]       // 楼长
    [InlineData("super_admin", true)] // 超级管理员
    [InlineData("student", false)]    // 学生 → 403
    [InlineData("counselor", false)]  // 未授权角色 → 403
    public async Task RepairStaffPolicy_AllowsRepairmanManagerAndSuperAdmin(string role, bool expected)
    {
        var principal = CreatePrincipal(role);

        var allowed = await EvaluateAsync(principal, AuthPolicies.RepairStaff);

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public async Task RepairStaffPolicy_RejectsAnonymousUser()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var allowed = await EvaluateAsync(anonymous, AuthPolicies.RepairStaff);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("super_admin", true)]
    [InlineData("repairman", false)] // 维修员不属于宿管端写操作策略 → 403
    public async Task DormAdminPolicy_StillExcludesRepairman(string role, bool expected)
    {
        var principal = CreatePrincipal(role);

        var allowed = await EvaluateAsync(principal, AuthPolicies.DormAdmin);

        Assert.Equal(expected, allowed);
    }

    // ==================== 契约路由与策略挂载断言 ====================

    [Fact]
    public void Dorm26To28_Endpoints_UseRepairStaffPolicy()
    {
        AssertEndpointPolicy(nameof(SlaDispatchController.GetPendingTickets), AuthPolicies.RepairStaff);
        AssertEndpointPolicy(nameof(SlaDispatchController.Claim), AuthPolicies.RepairStaff);
        AssertEndpointPolicy(nameof(SlaDispatchController.Complete), AuthPolicies.RepairStaff);
    }

    [Fact]
    public void Internal_Endpoints_UseServiceKeyAuth()
    {
        AssertEndpointHasAttribute<ServiceKeyAuthAttribute>(nameof(SlaDispatchController.AssignTicket));
        AssertEndpointHasAttribute<ServiceKeyAuthAttribute>(nameof(SlaDispatchController.EscalateSla));
    }

    [Fact]
    public void EscalateSla_Route_MatchesSvcSched03Contract()
    {
        var route = typeof(SlaDispatchController)
            .GetMethod(nameof(SlaDispatchController.EscalateSla))!
            .GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>()
            .Single();

        Assert.Equal("internal/scheduler/sla-escalation", route.Template);
    }

    // ==================== DORM-26 列表（成功路径 + 越权） ====================

    [Fact]
    public async Task GetPendingTickets_ReturnsPagedResult_ForMatchingAdmin()
    {
        var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 7, LoginName = "repair-01", PasswordHash = "h",
            AccountStatus = "正常", AdminId = "A001"
        });
        await context.SaveChangesAsync();

        var fake = new FakeSlaDispatchService
        {
            PendingResult = new PagedResult<PendingRepairTicketDto>
            {
                Items = new List<PendingRepairTicketDto>(),
                Total = 0, Page = 1, PageSize = 20
            }
        };
        var controller = CreateController(fake, context);
        SetUser(controller, accountId: "7", role: "repairman");

        var result = await controller.GetPendingTickets("A001", 1, 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<PagedResult<PendingRepairTicketDto>>>(ok.Value);
        Assert.Equal(200, response.Code);
        Assert.Equal("A001", fake.LastListAdminId);
        Assert.Equal(1, fake.LastPage);
        Assert.Equal(20, fake.LastPageSize);
    }

    [Fact]
    public async Task GetPendingTickets_RejectsOtherAdminsTickets()
    {
        var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 8, LoginName = "repair-02", PasswordHash = "h",
            AccountStatus = "正常", AdminId = "A002"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(new FakeSlaDispatchService(), context);
        SetUser(controller, accountId: "8", role: "repairman");

        var result = await controller.GetPendingTickets("A001", 1, 20);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(403, GetApiCode(ok.Value));
    }

    // ==================== DORM-28 repairResult 枚举校验 ====================

    [Theory]
    [InlineData("已修复")]
    [InlineData("需更换配件")]
    [InlineData("无法修复")]
    [InlineData(null)]
    public async Task Complete_AcceptsValidRepairResult(string? repairResult)
    {
        var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 9, LoginName = "repair-03", PasswordHash = "h",
            AccountStatus = "正常", AdminId = "A003"
        });
        await context.SaveChangesAsync();

        var fake = new FakeSlaDispatchService { ClaimResult = 0, CompleteResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, accountId: "9", role: "repairman");

        var result = await controller.Complete(1, new CompleteRepairRequest
        {
            Content = "已更换密封圈",
            RepairResult = repairResult
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(repairResult, fake.LastRepairResult);
    }

    [Fact]
    public async Task Complete_RejectsInvalidRepairResultEnum()
    {
        var fake = new FakeSlaDispatchService { CompleteResult = 0 };
        var controller = CreateController(fake, TestDbContextFactory.Create());
        SetUser(controller, accountId: "9", role: "repairman");

        var result = await controller.Complete(1, new CompleteRepairRequest
        {
            Content = "已更换密封圈",
            RepairResult = "fixed" // 契约枚举为中文：已修复/需更换配件/无法修复
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.False(fake.CompleteCalled); // 枚举不合法时不应触达服务层
    }

    // ==================== DORM-27 接单成功路径 ====================

    [Fact]
    public async Task Claim_ReturnsSuccess_WhenServiceAccepts()
    {
        var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 10, LoginName = "repair-04", PasswordHash = "h",
            AccountStatus = "正常", AdminId = "A004"
        });
        await context.SaveChangesAsync();

        var fake = new FakeSlaDispatchService { ClaimResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, accountId: "10", role: "repairman");

        var result = await controller.Claim(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(5, fake.LastClaimTicketId);
        Assert.Equal("A004", fake.LastClaimAdminId);
    }

    // ==================== 辅助 ====================

    /// <summary>ApiResponse&lt;T&gt; 无协变，空数据成功响应的具体泛型为匿名类型；按 Code 属性断言。</summary>
    private static int GetApiCode(object? value)
        => (int)(value!.GetType().GetProperty("Code")!.GetValue(value) ?? 0);

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Role, role)
        }, "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal principal, string policyName)
    {
        // 与 Program.cs 使用同一份 AuthPolicies.Register 注册逻辑，真实求值 RequireRole/RequireAuthenticatedUser
        var options = new AuthorizationOptions();
        AuthPolicies.Register(options);
        var policyProvider = new DefaultAuthorizationPolicyProvider(Options.Create(options));
        var handlerProvider = new DefaultAuthorizationHandlerProvider(
            new IAuthorizationHandler[] { new PassThroughAuthorizationHandler() });
        var service = new DefaultAuthorizationService(
            policyProvider, handlerProvider,
            NullLogger<DefaultAuthorizationService>.Instance,
            new DefaultAuthorizationHandlerContextFactory(),
            new DefaultAuthorizationEvaluator(),
            Options.Create(new AuthorizationOptions()));

        var result = await service.AuthorizeAsync(principal, null, policyName);
        return result.Succeeded;
    }

    private static SlaDispatchController CreateController(FakeSlaDispatchService service, AppDbContext context)
        => new(service, context);

    private static void SetUser(ControllerBase controller, string accountId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId),
            new Claim(ClaimTypes.Role, role)
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static void AssertEndpointPolicy(string methodName, string expectedPolicy)
    {
        var attribute = typeof(SlaDispatchController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute.Policy);
    }

    private static void AssertEndpointHasAttribute<TAttribute>(string methodName)
        where TAttribute : Attribute
    {
        Assert.NotNull(
            typeof(SlaDispatchController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(TAttribute), true)
                .SingleOrDefault());
    }

    private sealed class FakeSlaDispatchService : ISlaDispatchService
    {
        public PagedResult<PendingRepairTicketDto> PendingResult { get; set; } = new();
        public int ClaimResult { get; set; }
        public int CompleteResult { get; set; }

        public string? LastListAdminId { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public int LastClaimTicketId { get; private set; }
        public string? LastClaimAdminId { get; private set; }
        public string? LastRepairResult { get; private set; }
        public bool CompleteCalled { get; private set; }

        public Task<int> AssignTicket(int ticketId) => Task.FromResult(0);

        public Task<int> ClaimTicket(int ticketId, string adminId)
        {
            LastClaimTicketId = ticketId;
            LastClaimAdminId = adminId;
            return Task.FromResult(ClaimResult);
        }

        public Task<int> CompleteRepair(int ticketId, string adminId, string content, string? repairResult, DateTime? solveTime)
        {
            CompleteCalled = true;
            LastRepairResult = repairResult;
            return Task.FromResult(CompleteResult);
        }

        public Task EscalateSla() => Task.CompletedTask;

        public Task<PagedResult<PendingRepairTicketDto>> GetPendingTickets(string adminId, int page, int pageSize)
        {
            LastListAdminId = adminId;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(PendingResult);
        }
    }
}
