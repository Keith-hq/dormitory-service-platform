using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize]
[Route("api/students")]
public sealed class StudentProfileController : ControllerBase
{
    private readonly IStudentProfileService _service;

    public StudentProfileController(IStudentProfileService service)
    {
        _service = service;
    }

    /// <summary>STU-01 修改个人信息。</summary>
    [HttpPut("{studentId}/profile")]
    public async Task<ActionResult<ApiResponse<StudentProfileDto>>> UpdateProfile(
        string studentId,
        [FromBody] UpdateStudentProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateProfileAsync(studentId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "个人信息更新成功"));
    }

    /// <summary>STU-02 查看当前住宿信息。</summary>
    [HttpGet("{studentId}/accommodation")]
    public async Task<ActionResult<ApiResponse<AccommodationDto>>> GetCurrentAccommodation(
        string studentId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentAccommodationAsync(studentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-03 查看历史住宿记录。</summary>
    [HttpGet("{studentId}/accommodation/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccommodationDto>>>> GetAccommodationHistory(
        string studentId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAccommodationHistoryAsync(studentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
