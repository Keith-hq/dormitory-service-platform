using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize]
[Route("api/students/{studentId}/reports")]
public sealed class StudentReportController : ControllerBase
{
    private readonly IStudentReportService _service;

    public StudentReportController(IStudentReportService service)
    {
        _service = service;
    }

    /// <summary>REP-01 学生月度水电支出统计。</summary>
    [HttpGet("monthly-fee")]
    public async Task<ActionResult<ApiResponse<MonthlyFeeReportDto>>> GetMonthlyFee(
        string studentId,
        [FromQuery] MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetMonthlyFeeAsync(studentId, query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>REP-02 学生设施使用次数统计。</summary>
    [HttpGet("facility-usage")]
    public async Task<ActionResult<ApiResponse<FacilityUsageReportDto>>> GetFacilityUsage(
        string studentId,
        [FromQuery] MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetFacilityUsageAsync(studentId, query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>REP-03 年度生活报告。</summary>
    [HttpGet("annual")]
    public async Task<ActionResult<ApiResponse<AnnualReportDto>>> GetAnnualReport(
        string studentId,
        [FromQuery] AnnualReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAnnualReportAsync(studentId, query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
