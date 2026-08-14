using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 访客授权业务逻辑接口
/// </summary>
public interface IVisitorService
{
    Task<VisitorAuthorization> ApplyAsync(string studentId, VisitorApplyRequest dto);
    Task<PagedResult<VisitorAuthorization>> GetMyListAsync(string studentId, int page, int pageSize);
    Task<VisitorAuthorization> GetCredentialAsync(int authId, string currentStudentId);
    Task<VisitorAuthorization> RevokeAsync(int authId, string currentStudentId);
}

/// <summary>
/// 访客授权业务逻辑实现
/// </summary>
public class VisitorService : IVisitorService
{
    private readonly VisitorRepository _repository;

    public VisitorService(VisitorRepository repository)
    {
        _repository = repository;
    }

    public async Task<VisitorAuthorization> ApplyAsync(string studentId, VisitorApplyRequest dto)
    {
        var now = DateTime.Now;
        var endTime = dto.EndTime!.Value;
        if (endTime <= now)
            throw new BusinessException(400, "授权截止时间必须晚于当前时间");

        // Room_ID 为 NOT NULL：只读查询当前学生房间，无在住房间则不允许申请
        var roomId = await _repository.GetActiveRoomIdAsync(studentId)
            ?? throw new BusinessException(400, "当前无在住房间，无法申请访客授权");

        var auth = new VisitorAuthorization
        {
            StudentId = studentId,
            RoomId = roomId,
            VisitorName = dto.VisitorName,
            VisitReason = dto.VisitReason,
            AuthorizationToken = "VSR_" + Guid.NewGuid().ToString("N").ToUpper().Substring(0, 12),
            ExpiresTime = endTime,
            Status = "有效",
            CreateTime = now
        };
        return await _repository.AddAsync(auth);
    }

    public async Task<PagedResult<VisitorAuthorization>> GetMyListAsync(
        string studentId, int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedByStudentAsync(studentId, page, pageSize);
        return new PagedResult<VisitorAuthorization>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VisitorAuthorization> GetCredentialAsync(int authId, string currentStudentId)
    {
        var auth = await _repository.GetByIdAsync(authId)
            ?? throw new BusinessException(404, "授权记录不存在", StatusCodes.Status404NotFound);

        if (auth.StudentId != currentStudentId)
            throw new BusinessException(403, "无权查看他人的授权凭证", StatusCodes.Status403Forbidden);

        return auth;
    }

    public async Task<VisitorAuthorization> RevokeAsync(int authId, string currentStudentId)
    {
        var auth = await _repository.GetByIdAsync(authId)
            ?? throw new BusinessException(404, "授权记录不存在", StatusCodes.Status404NotFound);

        if (auth.StudentId != currentStudentId)
            throw new BusinessException(403, "无权撤销他人的授权", StatusCodes.Status403Forbidden);

        if (auth.Status != "有效")
            throw new BusinessException(400, $"当前状态为「{auth.Status}」，无法撤销");

        auth.Status = "已撤销";
        return await _repository.UpdateAsync(auth);
    }
}
