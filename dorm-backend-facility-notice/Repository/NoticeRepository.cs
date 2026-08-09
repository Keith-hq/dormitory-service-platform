using DormBackendFacilityNotice.Data;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Repository;

/// <summary>
/// 公告 Repository（继承 BaseRepository&lt;T&gt; 零代码获得 CRUD）
/// </summary>
public class NoticeRepository : BaseRepository<Notice>
{
    public NoticeRepository(AppDbContext context) : base(context) { }
}
