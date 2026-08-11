using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 公告业务逻辑接口
/// </summary>
public interface INoticeService
{
    Task<PagedResult<Notice>> GetPagedAsync(int page, int pageSize);
    Task<Notice?> GetByIdAsync(int id);
    Task<Notice> CreateAsync(NoticeCreateDto dto);
    Task<Notice?> UpdateAsync(int id, NoticeUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}
