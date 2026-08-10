using TemplateDormApi.Models;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

public class VoteService
{
    public RoomVote Create(CreateVoteRequest req)
    {
        return new RoomVote
        {
            
            VoteId = 0,
            RoomId = req.RoomId,
            InitiatorStudentId = req.InitiatorStudentId,
            Topic = req.Topic,
            CreateTime = DateTime.Now,
            Deadline = DateTime.Now.AddDays(req.DurationDays),
            EligibleCount = req.EligibleCount,
            Status = "进行中"
        };
    }
}