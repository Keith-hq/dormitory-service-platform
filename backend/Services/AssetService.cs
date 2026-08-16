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
    private readonly IAuditService _auditService;

    public AssetService(AppDbContext context, RepairRepository repairRepository, IAuditService auditService)
    {
        _context = context;
        _repairRepository = repairRepository;
        _auditService = auditService;
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

        var name = request.AssetName.Trim();
        if (name.Length == 0)
        {
            throw new BusinessException(400, "资产名称去除空白后不能为空", StatusCodes.Status400BadRequest);
        }
        if (request.Quantity > 999)
        {
            throw new BusinessException(400, "数量最大为 999（D_ASSET.Quantity 为 NUMBER(3)）", StatusCodes.Status400BadRequest);
        }

        var asset = new Asset
        {
            RoomId = request.RoomId,
            AssetName = name,
            Quantity = request.Quantity,
            Status = request.Status
        };
        _context.Assets.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);

        // DORM-17 预警来源：资产登记即标记为「损坏/缺失」时生成基础损耗预警。
        if (asset.Status is "损坏" or "缺失")
        {
            await EnsureWarningForDamagedOrMissingAsync(asset.AssetId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return ToDto(asset);
    }

    public async Task<AssetDto> UpdateAsync(int assetId, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await GetAssetAsync(assetId, cancellationToken);

        if (request.AssetName is not null)
        {
            var name = request.AssetName.Trim();
            if (name.Length == 0)
            {
                throw new BusinessException(400, "资产名称去除空白后不能为空", StatusCodes.Status400BadRequest);
            }
            asset.AssetName = name;
        }
        if (request.Status is not null)
        {
            ValidateStatus(request.Status);
            var wasDamagedOrMissing = asset.Status is "损坏" or "缺失";
            asset.Status = request.Status;
            // DORM-17 预警来源：状态变更为「损坏/缺失」时生成基础损耗预警（防重复）。
            if (!wasDamagedOrMissing && asset.Status is "损坏" or "缺失")
            {
                await EnsureWarningForDamagedOrMissingAsync(asset.AssetId, cancellationToken);
            }
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
        if (request.Quantity > 999)
        {
            throw new BusinessException(400, "数量最大为 999（D_ASSET.Quantity 为 NUMBER(3)）", StatusCodes.Status400BadRequest);
        }
        asset.Quantity = request.Quantity;

        // DORM-15「修改数量并填写盘点说明；写审计」：D_ASSET（foundation 冻结）无说明列，
        // 盘点说明经审计域公共服务写入 D_Audit_Event.DETAILS（含审计留痕）。
        var note = request.Note?.Trim();
        await _auditService.LogEventAsync(
            eventType: "STOCKTAKE",
            targetType: "Asset",
            targetId: assetId.ToString(),
            details: string.IsNullOrEmpty(note)
                ? $"盘点数量更新为 {request.Quantity}"
                : note);

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

        // 关系型（Oracle）下用显式事务保证「锁行 + 防重 + 工单 + 关联 + 预警」原子；
        // InMemory 测试环境不支持事务，IsRelational 守卫下跳过（同 CreditRepository 先例）。
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            // DORM-16 并发防重：对资产行加悲观锁，第二个并发请求在此等待锁释放后，
            // 重查未完结工单会命中 hasOpenTicket 而抛 409，避免同资产双工单。
            if (_context.Database.IsRelational())
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT Asset_ID FROM D_Asset WHERE Asset_ID = {0} FOR UPDATE",
                    cancellationToken, asset.AssetId);
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

            // 报修域写入经 RepairRepository 隔离；工单先落库取得 Ticket_ID。
            var ticket = _repairRepository.CreateAssetTicket(asset.RoomId, request.Description);
            await _context.SaveChangesAsync(cancellationToken);

            // 预警防重复：状态变更（损坏）时已生成未处理预警则不再新增。
            await EnsureWarningForDamagedOrMissingAsync(asset.AssetId, cancellationToken);
            _context.AssetRepairs.Add(new AssetRepair { AssetId = asset.AssetId, TicketId = ticket.TicketId });
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var warning = await _context.AssetWarnings
                .Where(item => item.AssetId == asset.AssetId)
                .OrderByDescending(item => item.WarningId)
                .FirstAsync(cancellationToken);
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

    /// <summary>
    /// DORM-17 基础损耗预警来源：资产状态为「损坏/缺失」时，若无未处理预警则生成一条。
    /// 登记 / 修改 / 转报修共用；防重复：已有未处理预警（Handled='否'）则不重复新增，
    /// 处理后再损坏/缺失会重新进入预警列表。
    /// </summary>
    private async Task EnsureWarningForDamagedOrMissingAsync(int assetId, CancellationToken cancellationToken)
    {
        var hasPending = await _context.AssetWarnings
            .AnyAsync(item => item.AssetId == assetId && item.Handled == "否", cancellationToken);
        if (!hasPending)
        {
            _context.AssetWarnings.Add(new AssetWarning { AssetId = assetId });
        }
    }

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
