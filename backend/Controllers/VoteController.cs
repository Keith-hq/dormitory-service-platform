using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 房间投票接口（STU-28~31）。路由对齐 Apifox 契约 /room-votes。
/// </summary>
[ApiController]
[Route("api/room-votes")]
public class VoteController : ControllerBase
{
    private readonly IVoteService _service;
    private readonly AppDbContext _context;

    public VoteController(IVoteService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    /// <summary>从 JWT 解析当前学生的 Student_ID。</summary>
    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        return studentId ?? throw new BusinessException(401, "当前账户未关联学生身份");
    }

    /// <summary>STU-28 房间议题：查询某房间的投票列表（roomId 为路径参数，对齐契约，不分页）。</summary>
    [HttpGet("/api/rooms/{roomId:int}/votes")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<RoomVote>>>> GetRoomVotes(int roomId)
    {
        var result = await _service.GetRoomVotesAsync(roomId);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-29 发起投票。</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<RoomVote>>> Create([FromBody] CreateVoteRequest request)
    {
        var studentId = await ResolveStudentId();
        var vote = await _service.CreateVoteAsync(studentId, request);
        return Ok(ApiResponse.Created(vote));
    }

    /// <summary>STU-30 参与投票（一人一票）。</summary>
    [HttpPost("{voteId:int}/responses")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<VoteStatisticsDto>>> Respond(
        int voteId,
        [FromBody] SubmitVoteRequest request)
    {
        var studentId = await ResolveStudentId();
        var stats = await _service.VoteAsync(studentId, voteId, request);
        return Ok(ApiResponse.Ok(stats, "投票成功"));
    }

    /// <summary>STU-31 投票统计（对齐契约 GET /room-votes/{voteId}）。</summary>
    [HttpGet("{voteId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<VoteStatisticsDto>>> Statistics(int voteId)
    {
        var stats = await _service.GetStatisticsAsync(voteId);
        return Ok(ApiResponse.Ok(stats));
    }
}
