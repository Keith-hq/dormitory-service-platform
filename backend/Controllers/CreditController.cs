using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 学生端信用分查询接口。
/// </summary>
[ApiController]
[Authorize]
[Route("api/students/{studentId}/credit")]
public class CreditController : ControllerBase
{
    private readonly ICreditService _creditService;

    public CreditController(ICreditService creditService)
    {
        _creditService = creditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetView(
        string studentId,
        CancellationToken cancellationToken)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _creditService.GetViewAsync(
            studentId,
            accountId.Value,
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
