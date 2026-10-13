using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class InventoryService : IInventoryService
{
    private readonly WmsDbContext _db;

    public InventoryService(WmsDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<InventoryResponseDto>> QueryAsync(InventoryQueryDto query)
    {
        var q = _db.Inventories
            .Include(i => i.Product)
            .Include(i => i.Location)
                .ThenInclude(l => l.Zone)
            .AsQueryable();

        if (query.ProductId.HasValue)
            q = q.Where(i => i.ProductId == query.ProductId.Value);
        if (query.LocationId.HasValue)
            q = q.Where(i => i.LocationId == query.LocationId.Value);
        if (query.WarehouseId.HasValue)
            q = q.Where(i => i.Location.Zone.WarehouseId == query.WarehouseId.Value);
        if (query.Status.HasValue)
            q = q.Where(i => i.Status == query.Status.Value);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var items = await q
            .OrderBy(i => i.Product.Name).ThenBy(i => i.Location.LocationCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<InventoryResponseDto?> GetByIdAsync(Guid id)
    {
        var item = await _db.Inventories
            .Include(i => i.Product)
            .Include(i => i.Location)
                .ThenInclude(l => l.Zone)
            .FirstOrDefaultAsync(i => i.Id == id);
        return item?.ToDto();
    }

    public async Task<IEnumerable<InventoryTransactionResponseDto>> GetTransactionsAsync(Guid? productId, Guid? locationId, int take)
    {
        var q = _db.InventoryTransactions.Include(t => t.Product).Include(t => t.Location).Include(t => t.User).AsQueryable();
        if (productId.HasValue) q = q.Where(t => t.ProductId == productId.Value);
        if (locationId.HasValue) q = q.Where(t => t.LocationId == locationId.Value);
        var rows = await q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync();
        return rows.Select(t => t.ToDto()).ToList();
    }

    private IQueryable<Adjustment> WithRefs() => _db.Adjustments
        .Include(a => a.Product).Include(a => a.Location).Include(a => a.Creator).Include(a => a.Approver);

    public async Task<IEnumerable<AdjustmentResponseDto>> GetAdjustmentsAsync()
    {
        var adj = await WithRefs().OrderByDescending(a => a.CreatedAt).ToListAsync();
        return adj.Select(a => a.ToDto()).ToList();
    }

    private static decimal Resulting(AdjType type, decimal current, decimal qty) => type switch
    {
        AdjType.Positive => current + qty,
        AdjType.Negative => current - qty,
        AdjType.Recount => qty,
        _ => current
    };

    public async Task<AdjustmentResponseDto> CreateAdjustmentAsync(CreateAdjustmentDto dto, Guid userId)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId))
            throw new BusinessRuleException("Product not found.");
        if (!await _db.Locations.AnyAsync(l => l.Id == dto.LocationId))
            throw new BusinessRuleException("Location not found.");
        if (dto.AdjType != AdjType.Recount && dto.AdjQty <= 0)
            throw new BusinessRuleException("The adjustment quantity must be greater than zero.");

        var today = DateTime.UtcNow.Date;
        var seq = await _db.Adjustments.CountAsync(a => a.CreatedAt >= today && a.CreatedAt < today.AddDays(1));

        var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == dto.ProductId && i.LocationId == dto.LocationId);
        var qtyBefore = inv?.QtyOnHand ?? 0;
        var qtyAfter = Resulting(dto.AdjType, qtyBefore, dto.AdjQty);
        if (qtyAfter < 0)
            throw new BusinessRuleException($"The adjustment would make stock negative (on hand {qtyBefore:0.##}).");

        var adj = new Adjustment
        {
            Id = Guid.NewGuid(), AdjNumber = StockHelper.NextNumber("ADJ", seq), ProductId = dto.ProductId, LocationId = dto.LocationId,
            AdjType = dto.AdjType, QtyBefore = qtyBefore, AdjQty = dto.AdjQty, QtyAfter = qtyAfter, Reason = dto.Reason,
            CreatedBy = userId, Status = AdjStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        _db.Adjustments.Add(adj);
        await _db.SaveChangesAsync();

        return (await WithRefs().FirstAsync(a => a.Id == adj.Id)).ToDto();
    }

    public async Task<AdjustmentResponseDto?> ApproveAdjustmentAsync(Guid adjId, Guid userId, bool isAdmin = true)
    {
        var adj = await WithRefs().FirstOrDefaultAsync(a => a.Id == adjId);
        if (adj == null) return null;
        if (adj.Status != AdjStatus.Draft)
            throw new BusinessRuleException($"Only draft adjustments can be approved (this one is {adj.Status}).");
        if (adj.CreatedBy == userId && !isAdmin)
            throw new BusinessRuleException("You cannot approve an adjustment you created yourself.");

        var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == adj.ProductId && i.LocationId == adj.LocationId);
        var current = inv?.QtyOnHand ?? 0;
        // Recalculate against today's stock: the count may have moved since the draft was created.
        var after = Resulting(adj.AdjType, current, adj.AdjQty);
        if (after < 0)
            throw new BusinessRuleException($"The adjustment would make stock negative (on hand {current:0.##}).");
        if (inv != null && after < inv.QtyReserved)
            throw new BusinessRuleException($"Stock cannot drop below the {inv.QtyReserved:0.##} reserved for open orders.");

        if (inv == null)
        {
            inv = new Inventory
            {
                Id = Guid.NewGuid(), ProductId = adj.ProductId, LocationId = adj.LocationId, QtyOnHand = 0, QtyReserved = 0,
                UnitCost = 0, Status = InventoryStatus.Available
            };
            _db.Inventories.Add(inv);
        }
        inv.QtyOnHand = after;
        if (adj.AdjType == AdjType.Recount) inv.LastCountedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        adj.QtyBefore = current;
        adj.QtyAfter = after;
        adj.Status = AdjStatus.Approved;
        adj.ApprovedBy = userId;
        StockHelper.LogTransaction(_db, adj.ProductId, adj.LocationId, ReferenceType.Adjustment, adj.Id,
            TransactionType.Adjust, current, after, after - current, userId, inv.LotNumber, adj.Reason);

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Stock changed while approving. Please retry."); }
        await StockHelper.SyncLocationStatusAsync(_db, adj.LocationId);

        return (await WithRefs().FirstAsync(a => a.Id == adjId)).ToDto();
    }

    public async Task<AdjustmentResponseDto?> RejectAdjustmentAsync(Guid adjId, Guid userId)
    {
        var adj = await WithRefs().FirstOrDefaultAsync(a => a.Id == adjId);
        if (adj == null) return null;
        if (adj.Status != AdjStatus.Draft)
            throw new BusinessRuleException($"Only draft adjustments can be rejected (this one is {adj.Status}).");
        adj.Status = AdjStatus.Cancelled;
        adj.ApprovedBy = userId;
        await _db.SaveChangesAsync();
        return (await WithRefs().FirstAsync(a => a.Id == adjId)).ToDto();
    }
}
