using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 保洁任务接口（DORM-39/40，宿管端）。
/// 任务生成（SVC-SCHED-04）属兰皓衍调度域，本控制器仅实现列表与完成。
/// </summary>
[ApiController]
[Route("api")]
public sealed class CleaningTaskController : ControllerBase
{
    private readonly ICleaningTaskService _service;

    public CleaningTaskController(ICleaningTaskService service)
    {
        _service = service;
    }

    /// <summary>保洁任务列表（DORM-39，分页）</summary>
    [HttpGet("cleaning-tasks")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<CleaningTaskDto>>>> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var data = await _service.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse.Ok(data));
    }

    /// <summary>完成保洁任务（DORM-40，幂等）</summary>
    [HttpPut("cleaning-tasks/{taskId}/complete")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<CleaningTaskDto>>> Complete(
        int taskId, CancellationToken cancellationToken = default)
    {
        var data = await _service.CompleteAsync(taskId, cancellationToken);
        return Ok(ApiResponse.Ok(data, "已完成"));
    }
}
