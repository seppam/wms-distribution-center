using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Return;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class ReturnService : IReturnService
{
    private readonly WmsDbContext _db;

    public ReturnService(WmsDbContext db) => _db = db;

    private IQueryable<ReturnOrder> WithRefs() => _db.ReturnOrders
        .Include(r => r.Order).Include(r => r.Warehouse).Include(r => r.Product)
        .Include(r => r.RestockLocation).Include(r => r.Receiver);

    public async Task<IEnumerable<ReturnResponseDto>> GetAllAsync()
    {
        var rows = await WithRefs().ToListAsync();
        return rows.OrderByDescending(r => r.ReceivedAt).Select(r => r.ToDto()).ToList();
    }

    public async Task<ReturnResponseDto?> GetByIdAsync(Guid id) => (await WithRefs().FirstOrDefaultAsync(r => r.Id == id))?.ToDto();

    public async Task<ReturnResponseDto> CreateAsync(CreateReturnDto dto, Guid userId)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.IsActive))
            throw new BusinessRuleException("Warehouse not found or inactive.");
        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId))
            throw new BusinessRuleException("Product not found.");

        if (dto.OrderId.HasValue)
        {
            var order = await _db.OutboundOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == dto.OrderId)
                ?? throw new BusinessRuleException("Order not found.");
            if (order.Status is not (OrderStatus.Shipped or OrderStatus.Completed))
                throw new BusinessRuleException("Only shipped orders can have returns.");
            var line = order.Items.FirstOrDefault(i => i.ProductId == dto.ProductId)
                ?? throw new BusinessRuleException("The product is not on that order.");
            var alreadyReturned = await _db.ReturnOrders
                .Where(r => r.OrderId == order.Id && r.ProductId == dto.ProductId)
                .SumAsync(r => (decimal?)r.Qty) ?? 0;
            if (alreadyReturned + dto.Qty > line.ShippedQty)
                throw new BusinessRuleException(
                    $"Return exceeds the shipped quantity: shipped {line.ShippedQty:0.##}, already returned {alreadyReturned:0.##}.");
        }

        var today = DateTime.UtcNow.Date;
        var seq = await _db.ReturnOrders.CountAsync(r => r.ReceivedAt >= today && r.ReceivedAt < today.AddDays(1));
        var ret = new ReturnOrder
        {
            Id = Guid.NewGuid(), ReturnNo = StockHelper.NextNumber("RET", seq), OrderId = dto.OrderId, WarehouseId = dto.WarehouseId,
            ProductId = dto.ProductId, Qty = dto.Qty, Reason = dto.Reason, Condition = dto.Condition, Notes = dto.Notes,
            Status = ReturnStatus.Received, ReceivedBy = userId, ReceivedAt = DateTime.UtcNow
        };
        _db.ReturnOrders.Add(ret);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(ret.Id))!;
    }

    public async Task<ReturnResponseDto?> ProcessAsync(Guid id, ProcessReturnDto dto, Guid userId)
    {
        var ret = await _db.ReturnOrders.FirstOrDefaultAsync(r => r.Id == id);
        if (ret == null) return null;
        if (ret.Status is ReturnStatus.Processed or ReturnStatus.Completed)
            throw new BusinessRuleException("This return has already been processed.");

        if (dto.Disposition == Disposition.Restock)
        {
            if (ret.Condition != ReturnCondition.Good)
                throw new BusinessRuleException("Only items returned in good condition can be restocked.");
            var locationId = dto.RestockLocationId ?? throw new BusinessRuleException("Choose a location to restock into.");
            var location = await _db.Locations.Include(l => l.Zone).FirstOrDefaultAsync(l => l.Id == locationId)
                ?? throw new BusinessRuleException("Restock location not found.");
            if (location.Zone.WarehouseId != ret.WarehouseId)
                throw new BusinessRuleException("The restock location belongs to a different warehouse.");
            if (location.Status == LocationStatus.Locked)
                throw new BusinessRuleException("The restock location is locked.");

            var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == ret.ProductId && i.LocationId == locationId);
            if (inv == null)
            {
                inv = new Inventory
                {
                    Id = Guid.NewGuid(), ProductId = ret.ProductId, LocationId = locationId, QtyOnHand = 0, QtyReserved = 0,
                    UnitCost = 0, Status = InventoryStatus.Available
                };
                _db.Inventories.Add(inv);
            }
            var before = inv.QtyOnHand;
            inv.QtyOnHand += ret.Qty;
            StockHelper.LogTransaction(_db, ret.ProductId, locationId, ReferenceType.Return, ret.Id,
                TransactionType.In, before, inv.QtyOnHand, ret.Qty, userId, inv.LotNumber, ret.ReturnNo);
            ret.RestockLocationId = locationId;
        }

        ret.Disposition = dto.Disposition;
        ret.ProcessedBy = userId;
        ret.ProcessedAt = DateTime.UtcNow;
        ret.Status = ReturnStatus.Completed;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) ret.Notes = string.IsNullOrWhiteSpace(ret.Notes) ? dto.Notes : $"{ret.Notes}\n{dto.Notes}";

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Stock changed while processing the return. Please retry."); }
        if (ret.RestockLocationId.HasValue) await StockHelper.SyncLocationStatusAsync(_db, ret.RestockLocationId.Value);
        return await GetByIdAsync(id);
    }
}
