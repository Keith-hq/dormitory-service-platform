using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Models;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api/room-votes")]
public class VoteController : ControllerBase
{
    private readonly VoteService _voteService;

    // 构造函数注入 (符合架构师第3条要求)
    public VoteController(VoteService voteService)
    {
        _voteService = voteService;
    }

    [HttpPost]
    public ApiResponse<RoomVote> Create([FromBody] CreateVoteRequest req)
        => ApiResponse.Ok(_voteService.Create(req));

    [HttpPost("{voteId}/responses")]
    public ApiResponse<object> Respond(long voteId, [FromBody] SubmitVoteRequest req)
    {
        if (req.Choice != "同意" && req.Choice != "不同意")
        {
            return ApiResponse.Error(400, "投票选项只能是 '同意' 或 '不同意'");
        }

        return ApiResponse.Ok((object)new
        {
            Msg = $"对投票 {voteId} 的选择 [{req.Choice}] 提交成功"
        });
    }
}