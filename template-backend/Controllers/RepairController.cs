using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class RepairController : ControllerBase
{
    private readonly IRepairService _service;

    public RepairController(IRepairService service)
    {
        _service = service;
    }

    /// <summary>STU-08 提交报修工单。</summary>
    [HttpPost("repair-tickets")]
    public async Task<ActionResult<ApiResponse<RepairTicketDto>>> Create(
        [FromBody] SubmitRepairTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "报修工单提交成功"));
    }

    /// <summary>STU-09 查询我的工单列表。</summary>
    [HttpGet("students/{studentId}/repair-tickets")]
    public async Task<ActionResult<ApiResponse<PagedResult<RepairTicketDto>>>> GetStudentTickets(
        string studentId,
        [FromQuery] RepairTicketQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetStudentTicketsAsync(studentId, query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-10 查询工单详情与进度。</summary>
    [HttpGet("repair-tickets/{ticketId:long}")]
    public async Task<ActionResult<ApiResponse<RepairTicketDto>>> GetById(
        long ticketId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(ticketId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-11 撤销工单。</summary>
    [HttpPost("repair-tickets/{ticketId:long}/cancel")]
    public async Task<ActionResult<ApiResponse<RepairTicketDto>>> Cancel(
        long ticketId,
        CancellationToken cancellationToken)
    {
        var result = await _service.CancelAsync(ticketId, cancellationToken);
        return Ok(ApiResponse.Ok(result, "工单撤销成功"));
    }

    /// <summary>STU-12 上传报修图片。</summary>
    [HttpPost("repair-tickets/{ticketId:long}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RepairAttachmentDto>>>> AddAttachments(
        long ticketId,
        [FromForm] UploadRepairAttachmentsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.AddAttachmentsAsync(ticketId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "附件上传成功"));
    }
}
