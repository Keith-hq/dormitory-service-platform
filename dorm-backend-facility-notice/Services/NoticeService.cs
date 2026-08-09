using DormBackendFacilityNotice.Repository;

namespace DormBackendFacilityNotice.Services;

/// <summary>
/// 公告业务逻辑实现（骨架，待实现）
/// </summary>
public class NoticeService : INoticeService
{
    private readonly NoticeRepository _repository;

    public NoticeService(NoticeRepository repository)
    {
        _repository = repository;
    }
}
