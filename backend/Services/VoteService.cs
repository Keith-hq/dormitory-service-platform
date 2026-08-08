using TemplateDormApi.Models;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

public class VoteService
{
    public RoomVote Create(CreateVoteRequest req)
    {
        return new RoomVote
        {
            VoteId = DateTime.Now.Ticks % 1000000,
            RoomId = req.RoomId,
            Title = req.Title,
            Status = "进行中"
        };
    }
}