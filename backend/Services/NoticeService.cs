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

    public NoticeService(NoticeRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<Notice>> GetPagedAsync(int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedAsync(page, pageSize);
        return new PagedResult<Notice>
        {
            Items = items,
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
            PublishTime = DateTime.Now
        };

        // 置顶：发布时同步写入 D_Notice_Display（1:1，共享主键由 EF 传播）
        if (dto.IsPinned == "是")
        {
            notice.Display = new NoticeDisplay { IsPinned = "是", PinTime = DateTime.Now };
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
                notice.Display ??= new NoticeDisplay { NoticeId = id, IsPinned = "是", PinTime = DateTime.Now };
                notice.Display.IsPinned = "是";
            }
            else if (notice.Display != null)
            {
                notice.Display.IsPinned = "否";
            }
        }

        return await _repository.UpdateAsync(notice);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repository.DeleteAsync(id);
}
