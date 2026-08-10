namespace TemplateDormApi.Security;

/// <summary>
/// 授权策略名与角色约定。
/// 角色 claim 取值沿用前端 mock 的英文小写约定（student / admin / counselor / super_admin）；
/// 登录模块落地时须按此签发 role claim，若有变更在此统一调整。
/// </summary>
public static class AuthPolicies
{
    /// <summary>宿管端写操作策略：要求已登录且角色为宿管/超级管理员。</summary>
    public const string DormAdmin = "DormAdmin";

    /// <summary>DormAdmin 策略允许的角色（宿管 / 超级管理员）。</summary>
    public const string DormAdminRoles = "admin,super_admin";
}
