using DormBackendFacilityNotice.Data;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Repository;

/// <summary>
/// 公共设施 Repository（继承 BaseRepository&lt;T&gt; 零代码获得 CRUD）
/// </summary>
public class FacilityRepository : BaseRepository<Facility>
{
    public FacilityRepository(AppDbContext context) : base(context) { }
}
