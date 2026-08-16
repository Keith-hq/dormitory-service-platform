using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>资产管理（DORM-12~18，刘鸿铭域）</summary>
public interface IAssetService
{
    Task<List<AssetDto>> GetByRoomAsync(int roomId, CancellationToken cancellationToken);

    Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken);

    Task<AssetDto> UpdateAsync(int assetId, UpdateAssetRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int assetId, CancellationToken cancellationToken);

    Task<AssetDto> StocktakeAsync(int assetId, StocktakeAssetRequest request, CancellationToken cancellationToken);

    Task<AssetWarningDto> ToRepairAsync(int assetId, ToRepairRequest request, CancellationToken cancellationToken);

    Task<PagedResult<AssetWarningDto>> GetWarningsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<AssetWarningDto> HandleWarningAsync(int assetId, HandleWarningRequest request, CancellationToken cancellationToken);
}
