using System.Security.Claims;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 公告业务逻辑实现
/// </summary>
public class NoticeService : INoticeService
{
    private readonly NoticeRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NoticeService(NoticeRepository repository, IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<NoticeItemDto>> GetPagedAsync(int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedAsync(page, pageSize);
        return new PagedResult<NoticeItemDto>
        {
            Items = items.Select(ToItemDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Notice?> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id);

    public async Task<Notice> CreateAsync(NoticeCreateDto dto)
    {
        var notice = new Notice
        {
            Title = dto.Title,
            Content = dto.Content,
            PublishTime = DateTime.Now,
            // 发布人取自 JWT（ClaimTypes.Name = 登录名），避免 D_Notice.Admin_ID 落 NULL
            // （NULL 会被 EF 读入非空 string 触发 ORA-50032 → 列表 500）
            AdminId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
                      ?? throw new UnauthorizedAccessException("无法识别当前登录用户")
        };

        // 置顶：041 起两列并入公告本体，发布时直写
        if (dto.IsPinned == "是")
        {
            notice.IsPinned = "是";
            notice.PinTime = DateTime.Now;
        }

        return await _repository.AddAsync(notice);
    }

    public async Task<Notice?> UpdateAsync(int id, NoticeUpdateDto dto)
    {
        var notice = await _repository.GetByIdAsync(id);
        if (notice == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Title))
            notice.Title = dto.Title;
        if (!string.IsNullOrWhiteSpace(dto.Content))
            notice.Content = dto.Content;

        if (!string.IsNullOrWhiteSpace(dto.IsPinned))
        {
            if (dto.IsPinned == "是")
            {
                // 取消后重新置顶时刷新置顶时间；保持置顶则不重置
                if (notice.IsPinned != "是")
                {
                    notice.PinTime = DateTime.Now;
                }
                notice.IsPinned = "是";
            }
            else
            {
                // 取消置顶：保留 PinTime 作为"曾置顶"痕迹（与迁移前卫星表语义一致）
                notice.IsPinned = "否";
            }
        }

        return await _repository.UpdateAsync(notice);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repository.DeleteAsync(id);

    private static NoticeItemDto ToItemDto(Notice notice)
        => new()
        {
            NoticeId = notice.NoticeId,
            AdminId = notice.AdminId,
            Title = notice.Title,
            Content = notice.Content,
            PublishTime = notice.PublishTime,
            IsPinned = notice.IsPinned,
            PinTime = notice.PinTime
        };
}
