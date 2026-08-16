using Microsoft.AspNetCore.Authorization;

namespace TemplateDormApi.Security;

/// <summary>
/// 授权策略名与角色约定。
/// role claim 取值沿用前端 mock 的英文小写约定，与 D_Admin.Role_Level 的映射如下：
///   admin       → 楼长（宿管端管理员）
///   repairman   → 维修员（难点⑤ DORM-26~28 的执行者）
///   super_admin → 超级管理员
///   student     → 学生（仅作身份，不进入宿管端策略）
/// 登录模块落地时须按此签发 role claim，若有变更在此统一调整。
/// </summary>
public static class AuthPolicies
{
    /// <summary>学生角色（student），对应学生端所有接口。</summary>
    public const string Student = "student";

    /// <summary>宿管角色（admin），对应宿管端读写接口。</summary>
    public const string Admin = "admin";

    /// <summary>辅导员角色（counselor），对应辅导员端接口。</summary>
    public const string Counselor = "counselor";

    /// <summary>超级管理员角色（super_admin），拥有全部管理权限。</summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>宿管端写操作策略：要求已登录且角色为宿管/超级管理员。</summary>
    public const string DormAdmin = "DormAdmin";

    /// <summary>DormAdmin 策略允许的角色（宿管 / 超级管理员）。</summary>
    public const string DormAdminRoles = "admin,super_admin";

    /// <summary>报修派单策略（DORM-26~28）：要求已登录且角色为楼长/维修员/超级管理员。</summary>
    public const string RepairStaff = "RepairStaff";

    /// <summary>RepairStaff 策略允许的角色（楼长 / 维修员 / 超级管理员）。</summary>
    public const string RepairStaffRoles = "admin,repairman,super_admin";

    /// <summary>
    /// 统一注册所有策略（Program.cs 与授权单元测试共用同一份定义，避免两处漂移）。
    /// </summary>
    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(DormAdmin, policy =>
            policy.RequireAuthenticatedUser().RequireRole("admin", "super_admin"));

        options.AddPolicy(RepairStaff, policy =>
            policy.RequireAuthenticatedUser().RequireRole("admin", "repairman", "super_admin"));
    }
}
