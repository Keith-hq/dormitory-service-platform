using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 资产管理实现（DORM-12~18）。
/// D_ASSET 属 foundation 冻结表，预警/关联痕迹通过扩展表 D_Asset_Repair /
/// D_Asset_Warning 记录；D_Repair_Ticket 写入经 RepairRepository 隔离（报修域拥有）。
/// </summary>
public sealed class AssetService : IAssetService
{
    private static readonly string[] ValidStatuses = { "正常", "损坏", "缺失" };

    private readonly AppDbContext _context;
    private readonly RepairRepository _repairRepository;

    public AssetService(AppDbContext context, RepairRepository repairRepository)
    {
        _context = context;
        _repairRepository = repairRepository;
    }

    public async Task<List<AssetDto>> GetByRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        return await _context.Assets
            .AsNoTracking()
            .Where(item => item.RoomId == roomId)
            .OrderBy(item => item.AssetId)
            .Select(item => new AssetDto
            {
                AssetId = item.AssetId,
                RoomId = item.RoomId,
                AssetName = item.AssetName,
                Quantity = item.Quantity,
                Status = item.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        if (!await _context.Rooms.AnyAsync(item => item.RoomId == request.RoomId, cancellationToken))
        {
            throw new BusinessException(404, "所属房间不存在", StatusCodes.Status404NotFound);
        }
        ValidateStatus(request.Status);

        var asset = new Asset
        {
            RoomId = request.RoomId,
            AssetName = request.AssetName.Trim(),
            Quantity = request.Quantity,
            Status = request.Status
        };
        _context.Assets.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(asset);
    }

    public async Task<AssetDto> UpdateAsync(int assetId, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await GetAssetAsync(assetId, cancellationToken);

        if (request.AssetName is not null)
        {
            asset.AssetName = request.AssetName.Trim();
        }
        if (request.Status is not null)
        {
            ValidateStatus(request.Status);
            asset.Status = request.Status;
        }
        // remark 契约可选、D_ASSET（foundation 冻结）无对应列，不落库。

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(asset);
    }

    public async Task DeleteAsync(int assetId, CancellationToken cancellationToken)
    {
        var asset = await GetAssetAsync(assetId, cancellationToken);

        var hasRepairLink = await _context.AssetRepairs
            .AnyAsync(item => item.AssetId == assetId, cancellationToken);
        if (hasRepairLink)
        {
            throw new BusinessException(409, "已关联报修的资产禁止删除", StatusCodes.Status409Conflict);
        }

        _context.Assets.Remove(asset);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssetDto> StocktakeAsync(int assetId, StocktakeAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await GetAssetAsync(assetId, cancellationToken);

        if (request.Quantity < 0)
        {
            throw new BusinessException(400, "数量必须大于等于 0", StatusCodes.Status400BadRequest);
        }
        asset.Quantity = request.Quantity;
        // note 契约可选、D_ASSET（foundation 冻结）无对应列，不落库。

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(asset);
    }

    public async Task<AssetWarningDto> ToRepairAsync(int assetId, ToRepairRequest request, CancellationToken cancellationToken)
    {
        var asset = await GetAssetAsync(assetId, cancellationToken);

        if (asset.Status != "损坏")
        {
            throw new BusinessException(409, "仅状态为「损坏」的资产可转报修；缺失不可转", StatusCodes.Status409Conflict);
        }

        var hasOpenTicket = await _context.AssetRepairs
            .AsNoTracking()
            .Join(_context.RepairTickets,
                link => link.TicketId,
                ticket => ticket.TicketId,
                (link, ticket) => new { link.AssetId, TicketStatus = ticket.Status })
            .AnyAsync(item => item.AssetId == assetId
                && item.TicketStatus != "已完成"
                && item.TicketStatus != "已撤销", cancellationToken);
        if (hasOpenTicket)
        {
            throw new BusinessException(409, "该资产已有未完结的报修工单，不能重复转报修", StatusCodes.Status409Conflict);
        }

        // 关系型（Oracle）下用显式事务保证「工单 + 关联 + 预警」原子；InMemory
        // 测试环境不支持事务，IsRelational 守卫下跳过（同 CreditRepository 先例）。
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            // 报修域写入经 RepairRepository 隔离；工单先落库取得 Ticket_ID。
            var ticket = _repairRepository.CreateAssetTicket(asset.RoomId, request.Description);
            await _context.SaveChangesAsync(cancellationToken);

            var warning = new AssetWarning { AssetId = asset.AssetId };
            _context.AssetWarnings.Add(warning);
            _context.AssetRepairs.Add(new AssetRepair { AssetId = asset.AssetId, TicketId = ticket.TicketId });
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return ToWarningDto(warning, asset.AssetName, asset.RoomId);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task<PagedResult<AssetWarningDto>> GetWarningsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.AssetWarnings
            .AsNoTracking()
            .Where(item => item.Handled == "否");

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.WarningId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_context.Assets.AsNoTracking(),
                warning => warning.AssetId,
                asset => asset.AssetId,
                (warning, asset) => new AssetWarningDto
                {
                    WarningId = warning.WarningId,
                    AssetId = warning.AssetId,
                    AssetName = asset.AssetName,
                    RoomId = asset.RoomId,
                    CreateTime = warning.CreateTime,
                    Note = warning.Note,
                    HandleAction = warning.HandleAction,
                    HandleTime = warning.HandleTime,
                    Handled = warning.Handled
                })
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetWarningDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AssetWarningDto> HandleWarningAsync(int assetId, HandleWarningRequest request, CancellationToken cancellationToken)
    {
        var warning = await _context.AssetWarnings
            .Where(item => item.AssetId == assetId && item.Handled == "否")
            .OrderByDescending(item => item.WarningId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(404, "该资产没有待处理的损耗预警", StatusCodes.Status404NotFound);

        warning.HandleAction = request.Action;
        warning.HandleTime = DateTime.Now;
        warning.Handled = "是";
        if (request.Note is not null)
        {
            warning.Note = request.Note;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var asset = await _context.Assets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.AssetId == assetId, cancellationToken);
        return ToWarningDto(warning, asset?.AssetName ?? string.Empty, asset?.RoomId);
    }

    private async Task<Asset> GetAssetAsync(int assetId, CancellationToken cancellationToken)
        => await _context.Assets.FirstOrDefaultAsync(item => item.AssetId == assetId, cancellationToken)
            ?? throw new BusinessException(404, "资产不存在", StatusCodes.Status404NotFound);

    private static void ValidateStatus(string status)
    {
        if (!ValidStatuses.Contains(status))
        {
            throw new BusinessException(400, $"状态不合法，只能是：{string.Join(" / ", ValidStatuses)}", StatusCodes.Status400BadRequest);
        }
    }

    private static AssetDto ToDto(Asset item) => new()
    {
        AssetId = item.AssetId,
        RoomId = item.RoomId,
        AssetName = item.AssetName,
        Quantity = item.Quantity,
        Status = item.Status
    };

    private static AssetWarningDto ToWarningDto(AssetWarning item, string assetName, int? roomId) => new()
    {
        WarningId = item.WarningId,
        AssetId = item.AssetId,
        AssetName = assetName,
        RoomId = roomId,
        CreateTime = item.CreateTime,
        Note = item.Note,
        HandleAction = item.HandleAction,
        HandleTime = item.HandleTime,
        Handled = item.Handled
    };
}
