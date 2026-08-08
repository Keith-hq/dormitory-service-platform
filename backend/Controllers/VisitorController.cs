using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Models;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api")]
public class VisitorController : ControllerBase
{
    private readonly VisitorService _service = new();

    // POST /api/visitor-authorizations
    [HttpPost("visitor-authorizations")]
    public ApiResponse<VisitorAuthorization> Post([FromBody] VisitorApplyRequest req)
        => ApiResponse.Ok(_service.Apply(req));

    // GET /api/students/{studentId}/visitor-authorizations
    [HttpGet("students/{studentId}/visitor-authorizations")]
    public ApiResponse<List<VisitorAuthorization>> Get(string studentId)
        => ApiResponse.Ok(_service.GetList(studentId));
}