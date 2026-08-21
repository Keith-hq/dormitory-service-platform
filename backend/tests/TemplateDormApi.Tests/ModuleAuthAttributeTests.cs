using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using TemplateDormApi.Controllers;
using TemplateDormApi.Security;

namespace TemplateDormApi.Tests;

/// <summary>
/// 模块接口鉴权装配审计（评审整改 S2，WalletControllerTests.Endpoints_RequireAuthentication 同范式）：
/// - 学生自助接口：仅 [Authorize]（无策略）→ 匿名 401；归属校验在服务层（跨账号 403）
/// - 宿管端接口：Policy = DormAdmin（admin/super_admin）→ 学生 token 403
/// 401/403 行为级验证见 ModuleAuthBehaviorTests（TestWebApplicationFactory 全链路）。
/// </summary>
public class ModuleAuthAttributeTests
{
    /// <summary>取类型（methodName 为 null）或方法上的 [Authorize]（不含继承类级属性）</summary>
    private static AuthorizeAttribute? AuthOf(Type controllerType, string? methodName = null)
    {
        var member = methodName == null
            ? (MemberInfo)controllerType
            : controllerType.GetMethod(methodName)!;
        return member.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static void AssertPlainLogin(Type controllerType, params string[] methodNames)
    {
        foreach (var m in methodNames)
        {
            var attr = AuthOf(controllerType, m);
            Assert.True(attr != null, $"{controllerType.Name}.{m} 缺少 [Authorize]（匿名可达，401 缺失）");
            Assert.True(string.IsNullOrEmpty(attr.Policy), $"{controllerType.Name}.{m} 不应绑定角色策略");
        }
    }

    private static void AssertDormAdmin(Type controllerType, params string[] methodNames)
    {
        foreach (var m in methodNames)
        {
            var attr = AuthOf(controllerType, m);
            Assert.True(attr != null, $"{controllerType.Name}.{m} 缺少 [Authorize]");
            Assert.Equal(AuthPolicies.DormAdmin, attr.Policy);
        }
    }

    private static void AssertRoles(Type controllerType, string roles, params string[] methodNames)
    {
        foreach (var m in methodNames)
        {
            var attr = AuthOf(controllerType, m);
            Assert.True(attr != null, $"{controllerType.Name}.{m} 缺少 [Authorize]");
            Assert.Equal(roles, attr.Roles);
        }
    }

    // ==================== 退宿清算（DORM-11/35~38） ====================

    [Fact]
    public void Checkout_ClassLevel_RequiresLoginOnly()
    {
        // 类级 [Authorize] 无策略：匿名 401；归属校验在服务层（非本人 403，宿管放行）
        var attr = AuthOf(typeof(CheckoutController));
        Assert.NotNull(attr);
        Assert.True(string.IsNullOrEmpty(attr!.Policy));
    }

    // ==================== 离校报备（COUN-01~04 / STU-15~17,40） ====================

    [Fact]
    public void Leave_CounselorEndpoints_RequireLeaveApprovalRoles()
        // COUN-01~04：辅导员端审批由 LeaveApprovalRoles（admin/super_admin/counselor）放行，
        // 见 AuthPolicies.LeaveApprovalRoles 与 LeaveController（f642cbf 修复辅导员 403）。
        => AssertRoles(typeof(LeaveController), AuthPolicies.LeaveApprovalRoles,
            nameof(LeaveController.List),
            nameof(LeaveController.Approve),
            nameof(LeaveController.Reject),
            nameof(LeaveController.Statistics));

    [Fact]
    public void Leave_StudentEndpoints_RequireLoginOnly()
        => AssertPlainLogin(typeof(LeaveController),
            nameof(LeaveController.Submit),
            nameof(LeaveController.MyList),
            nameof(LeaveController.Update),
            nameof(LeaveController.Cancel));

    // ==================== 住宿分配（DORM-08/09/10） ====================

    [Fact]
    public void Allocation_Create_RequiresLoginOnly()
        => AssertPlainLogin(typeof(BedAllocationController), nameof(BedAllocationController.Create));

    [Fact]
    public void Allocation_TransferAndOccupants_RequireDormAdmin()
        => AssertDormAdmin(typeof(BedAllocationController),
            nameof(BedAllocationController.Transfer),
            nameof(BedAllocationController.Occupants));

    // ==================== 房间/楼栋（宿管端） ====================

    [Fact]
    public void Room_ClassLevel_RequiresDormAdmin()
    {
        var attr = AuthOf(typeof(RoomController));
        Assert.NotNull(attr);
        Assert.Equal(AuthPolicies.DormAdmin, attr!.Policy);
    }

    [Fact]
    public void Building_AllEndpoints_RequireDormAdmin()
        => AssertDormAdmin(typeof(BuildingController),
            nameof(BuildingController.GetPaged),
            nameof(BuildingController.GetById),
            nameof(BuildingController.Create),
            nameof(BuildingController.Update),
            nameof(BuildingController.Delete));
}
