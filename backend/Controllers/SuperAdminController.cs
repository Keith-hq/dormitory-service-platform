using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("api/super-admin")]
public class SuperAdminController : ControllerBase
{
    // ===== SUPER-01: 获取系统概览统计 =====
    [HttpGet("dashboard")]
    public IActionResult GetDashboard()
    {
        // 占位数据，实际应调用 Service
        return Ok(ApiResponse.Ok(new
        {
            totalUsers = 100,
            totalRooms = 50,
            pendingRepairs = 3
        }));
    }

    // ===== SUPER-02: 获取所有管理员列表 =====
    [HttpGet("admins")]
    public IActionResult GetAdmins()
    {
        // 占位数据
        var admins = new[]
        {
            new { adminId = "admin001", name = "宿管张三", role = "admin" },
            new { adminId = "super001", name = "超级管理员", role = "super_admin" }
        };
        return Ok(ApiResponse.Ok(admins));
    }

    // ===== SUPER-03: 创建管理员 =====
    [HttpPost("admins")]
    public IActionResult CreateAdmin([FromBody] object newAdmin)
    {
        // 实际应验证并创建，这里模拟成功
        return Ok(ApiResponse.Ok(new { message = "管理员创建成功", adminId = "admin002" }));
    }

    // ===== SUPER-04: 更新管理员信息 =====
    [HttpPut("admins/{adminId}")]
    public IActionResult UpdateAdmin(string adminId, [FromBody] object updateData)
    {
        return Ok(ApiResponse.Ok(new { message = $"管理员 {adminId} 更新成功" }));
    }

    // ===== SUPER-05: 删除管理员 =====
    [HttpDelete("admins/{adminId}")]
    public IActionResult DeleteAdmin(string adminId)
    {
        return Ok(ApiResponse.Ok(new { message = $"管理员 {adminId} 删除成功" }));
    }

    // ===== SUPER-06: 系统配置管理（获取） =====
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(ApiResponse.Ok(new { maintenanceMode = false, maxLoginAttempts = 5 }));
    }

    // ===== SUPER-07: 更新系统配置 =====
    [HttpPut("settings")]
    public IActionResult UpdateSettings([FromBody] object settings)
    {
        return Ok(ApiResponse.Ok(new { message = "系统配置已更新" }));
    }

    // ===== PWD-02: 重置用户密码（超管特权） =====
    [HttpPost("users/{userId}/reset-password")]
    public IActionResult ResetPassword(string userId)
    {
        return Ok(ApiResponse.Ok(new { message = $"用户 {userId} 密码已重置为临时密码" }));
    }

    // ===== VIOL-03: 查看所有违规记录 =====
    [HttpGet("violations")]
    public IActionResult GetViolations()
    {
        var violations = new[]
        {
            new { id = 1, studentId = "S001", description = "晚归", date = "2026-08-10" }
        };
        return Ok(ApiResponse.Ok(violations));
    }

    // ===== IMPORT-01: 批量导入数据 =====
    [HttpPost("import")]
    public IActionResult ImportData([FromBody] object importData)
    {
        return Ok(ApiResponse.Ok(new { message = "数据导入成功", importedCount = 10 }));
    }

    // ===== REPT-01: 生成报表 =====
    [HttpGet("reports/{reportType}")]
    public IActionResult GetReport(string reportType)
    {
        return Ok(ApiResponse.Ok(new { reportType, data = new { total = 100, details = "报表数据" } }));
    }
}