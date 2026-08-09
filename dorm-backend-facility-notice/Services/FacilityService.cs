using DormBackendFacilityNotice.Repository;

namespace DormBackendFacilityNotice.Services;

/// <summary>
/// 公共设施业务逻辑实现（骨架，待实现）
/// </summary>
public class FacilityService : IFacilityService
{
    private readonly FacilityRepository _repository;

    public FacilityService(FacilityRepository repository)
    {
        _repository = repository;
    }
}
