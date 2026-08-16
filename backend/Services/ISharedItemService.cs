using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>共享物品主数据（DORM-48/49；借还/库存扣减属李昂 InventoryTxn 域）</summary>
public interface ISharedItemService
{
    Task<SharedItemDto> CreateAsync(CreateSharedItemRequest request, CancellationToken cancellationToken);

    Task<SharedItemDto> UpdateAsync(int itemId, UpdateSharedItemRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int itemId, CancellationToken cancellationToken);
}
