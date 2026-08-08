using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Models;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api/room-votes")]
public class VoteController : ControllerBase
{
    private readonly VoteService _service = new();

    // POST /api/room-votes (发起投票)
    [HttpPost]
    public ApiResponse<RoomVote> Create([FromBody] CreateVoteRequest req)
        => ApiResponse.Ok(_service.Create(req));

    // POST /api/room-votes/{voteId}/responses (参与投票)
    [HttpPost("{voteId}/responses")]
    public ApiResponse<object> Respond(long voteId, [FromBody] SubmitVoteRequest req)
        => ApiResponse.Ok((object)new { Msg = $"投票{voteId}提交成功", YourOption = req.Option });
}