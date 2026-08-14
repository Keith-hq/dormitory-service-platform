namespace TemplateDormApi.Security;

/// <summary>
/// 授权策略名与角色约定。
/// 角色 claim 取值沿用前端 mock 的英文小写约定（student / admin / counselor / super_admin）；
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
}
