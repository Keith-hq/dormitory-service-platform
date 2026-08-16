using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 共享物品主数据实现（DORM-48/49）。
/// 库存扣减（借还）走李昂的 SP_Borrow_Item / SP_Return_Item，本服务只做主数据
/// 增改删；D_Shared_Item.Status='停用' 时 SP 侧拒绝借出（IT-C5-006）。
/// </summary>
public sealed class SharedItemService : ISharedItemService
{
    private readonly AppDbContext _context;

    public SharedItemService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SharedItemDto> CreateAsync(CreateSharedItemRequest request, CancellationToken cancellationToken)
    {
        if (!await _context.Buildings.AnyAsync(item => item.BuildingId == request.BuildingId, cancellationToken))
        {
            throw new BusinessException(404, "所属楼栋不存在", StatusCodes.Status404NotFound);
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new BusinessException(400, "物品名称去除空白后不能为空", StatusCodes.Status400BadRequest);
        }
        if (request.Quantity > 999)
        {
            throw new BusinessException(400, "数量最大为 999（D_Shared_Item.Total_Qty 为 NUMBER(3)）", StatusCodes.Status400BadRequest);
        }

        var item = new SharedItem
        {
            ItemName = name,
            BuildingId = request.BuildingId,
            TotalQty = request.Quantity,
            AvailableQty = request.Quantity,
            Status = "正常",
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        _context.SharedItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<SharedItemDto> UpdateAsync(int itemId, UpdateSharedItemRequest request, CancellationToken cancellationToken)
    {
        var item = await GetItemAsync(itemId, cancellationToken);

        if (request.Quantity is { } newTotal)
        {
            if (newTotal < 0)
            {
                throw new BusinessException(400, "数量必须大于等于 0", StatusCodes.Status400BadRequest);
            }
            if (newTotal > 999)
            {
                throw new BusinessException(400, "数量最大为 999（D_Shared_Item.Total_Qty 为 NUMBER(3)）", StatusCodes.Status400BadRequest);
            }
            if (newTotal < item.AvailableQty)
            {
                throw new BusinessException(400, "已有借出，总数不能低于当前可借数量", StatusCodes.Status400BadRequest);
            }
            item.TotalQty = newTotal;
        }

        if (request.Description is not null)
        {
            item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.Status is not null)
        {
            if (request.Status is not ("正常" or "停用"))
            {
                throw new BusinessException(400, "状态不合法，只能是：正常 / 停用", StatusCodes.Status400BadRequest);
            }
            item.Status = request.Status;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(int itemId, CancellationToken cancellationToken)
    {
        var item = await GetItemAsync(itemId, cancellationToken);

        // DORM-49 删除口径：仅「有未归还借出记录」时禁止删除；已归还的历史借出不阻断。
        var hasUnreturnedLoan = await _context.ItemLoans
            .AnyAsync(loan => loan.ItemId == itemId && loan.ReturnTime == null, cancellationToken);
        if (hasUnreturnedLoan)
        {
            throw new BusinessException(409, "有未归还借出记录时禁止删除", StatusCodes.Status409Conflict);
        }

        _context.SharedItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<SharedItem> GetItemAsync(int itemId, CancellationToken cancellationToken)
        => await _context.SharedItems.FirstOrDefaultAsync(item => item.ItemId == itemId, cancellationToken)
            ?? throw new BusinessException(404, "共享物品不存在", StatusCodes.Status404NotFound);

    private static SharedItemDto ToDto(SharedItem item) => new()
    {
        ItemId = item.ItemId,
        ItemName = item.ItemName,
        BuildingId = item.BuildingId,
        TotalQty = item.TotalQty,
        AvailableQty = item.AvailableQty,
        Status = item.Status,
        Description = item.Description
    };
}
