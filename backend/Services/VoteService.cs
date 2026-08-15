using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 房间投票业务逻辑接口
/// </summary>
public interface IVoteService
{
    Task<List<RoomVote>> GetRoomVotesAsync(int roomId);
    Task<RoomVote> CreateVoteAsync(string initiatorStudentId, CreateVoteRequest dto);
    Task<VoteStatisticsDto> VoteAsync(string studentId, int voteId, SubmitVoteRequest dto);
    Task<VoteStatisticsDto> GetStatisticsAsync(int voteId);
}

/// <summary>
/// 房间投票业务逻辑实现
/// </summary>
public class VoteService : IVoteService
{
    private readonly VoteRepository _repository;
    private readonly RoomRepository _roomRepository;

    public VoteService(VoteRepository repository, RoomRepository roomRepository)
    {
        _repository = repository;
        _roomRepository = roomRepository;
    }

    public async Task<List<RoomVote>> GetRoomVotesAsync(int roomId)
        => await _repository.GetByRoomAsync(roomId);

    public async Task<RoomVote> CreateVoteAsync(string initiatorStudentId, CreateVoteRequest dto)
    {
        if (await _roomRepository.GetByIdAsync(dto.RoomId) == null)
            throw new BusinessException(404, "房间不存在", StatusCodes.Status404NotFound);

        var now = DateTime.Now;
        // 对齐 DDL Deadline：不传截止时间时默认 3 天后
        var deadline = dto.Deadline ?? now.AddDays(3);
        if (deadline <= now)
            throw new BusinessException(400, "截止时间必须晚于当前时间");

        var vote = new RoomVote
        {
            RoomId = dto.RoomId,
            InitiatorStudentId = initiatorStudentId,
            Topic = dto.Topic,
            CreateTime = now,
            Deadline = deadline,
            EligibleCount = dto.EligibleCount,
            Status = "进行中"
        };
        return await _repository.AddAsync(vote);
    }

    public async Task<VoteStatisticsDto> VoteAsync(string studentId, int voteId, SubmitVoteRequest dto)
    {
        var vote = await _repository.GetByIdAsync(voteId)
            ?? throw new BusinessException(404, "投票不存在", StatusCodes.Status404NotFound);

        if (vote.Status != "进行中")
            throw new BusinessException(400, "该投票已结束，无法参与");

        if (DateTime.Now > vote.Deadline)
            throw new BusinessException(400, "该投票已超过截止时间");

        if (await _repository.HasRespondedAsync(voteId, studentId))
            throw new BusinessException(400, "您已参与过该投票，不能重复投票");

        await _repository.AddResponseAsync(new RoomVoteResponse
        {
            VoteId = voteId,
            StudentId = studentId,
            Choice = dto.Choice,
            VoteTime = DateTime.Now
        });

        return await GetStatisticsAsync(voteId);
    }

    public async Task<VoteStatisticsDto> GetStatisticsAsync(int voteId)
    {
        var vote = await _repository.GetByIdAsync(voteId)
            ?? throw new BusinessException(404, "投票不存在", StatusCodes.Status404NotFound);

        var (agree, disagree) = await _repository.CountChoicesAsync(voteId);
        return new VoteStatisticsDto
        {
            VoteId = vote.VoteId,
            Topic = vote.Topic,
            Status = vote.Status,
            EligibleCount = vote.EligibleCount,
            AgreeCount = agree,
            DisagreeCount = disagree,
            TotalCount = agree + disagree
        };
    }
}
