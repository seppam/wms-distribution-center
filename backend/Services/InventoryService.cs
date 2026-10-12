using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class InventoryService : IInventoryService
{
    private readonly WmsDbContext _db;
    private readonly IMapper _mapper;

    public InventoryService(WmsDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
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

        var items = await q
            .OrderBy(i => i.Product.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return _mapper.Map<IEnumerable<InventoryResponseDto>>(items);
    }

    public async Task<InventoryResponseDto?> GetByIdAsync(Guid id)
    {
        var item = await _db.Inventories
            .Include(i => i.Product)
            .Include(i => i.Location)
                .ThenInclude(l => l.Zone)
            .FirstOrDefaultAsync(i => i.Id == id);
        return item == null ? null : _mapper.Map<InventoryResponseDto>(item);
    }

    public async Task<IEnumerable<AdjustmentResponseDto>> GetAdjustmentsAsync()
    {
        var adj = await _db.Adjustments
            .Include(a => a.Product)
            .Include(a => a.Location)
            .Include(a => a.Creator)
            .Include(a => a.Approver)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return _mapper.Map<IEnumerable<AdjustmentResponseDto>>(adj);
    }

    public async Task<AdjustmentResponseDto> CreateAdjustmentAsync(CreateAdjustmentDto dto, Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        var seq = await _db.Adjustments
            .Where(a => a.CreatedAt >= today && a.CreatedAt < today.AddDays(1))
            .CountAsync() + 1;
        var adjNumber = $"ADJ-{today:yyyyMMdd}-{seq:D4}";

        var inv = await _db.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == dto.ProductId && i.LocationId == dto.LocationId);

        decimal qtyBefore = inv?.QtyOnHand ?? 0;
        decimal qtyAfter = dto.AdjType switch
        {
            AdjType.Positive => qtyBefore + dto.AdjQty,
            AdjType.Negative => qtyBefore - dto.AdjQty,
            AdjType.Recount => dto.AdjQty,
            _ => qtyBefore
        };

        var adj = new Adjustment
        {
            Id = Guid.NewGuid(),
            AdjNumber = adjNumber,
            ProductId = dto.ProductId,
            LocationId = dto.LocationId,
            AdjType = dto.AdjType,
            QtyBefore = qtyBefore,
            AdjQty = dto.AdjQty,
            QtyAfter = qtyAfter,
            Reason = dto.Reason,
            CreatedBy = userId,
            Status = AdjStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.Adjustments.Add(adj);
        await _db.SaveChangesAsync();

        await _db.Entry(adj).Reference(a => a.Product).LoadAsync();
        await _db.Entry(adj).Reference(a => a.Location).LoadAsync();
        await _db.Entry(adj).Reference(a => a.Creator).LoadAsync();

        return _mapper.Map<AdjustmentResponseDto>(adj);
    }

    public async Task<AdjustmentResponseDto?> ApproveAdjustmentAsync(Guid adjId, Guid userId)
    {
        var adj = await _db.Adjustments
            .Include(a => a.Product)
            .Include(a => a.Location)
            .Include(a => a.Creator)
            .Include(a => a.Approver)
            .FirstOrDefaultAsync(a => a.Id == adjId);

        if (adj == null || adj.Status != AdjStatus.Draft) return null;

        adj.Status = AdjStatus.Approved;
        adj.ApprovedBy = userId;

        // Apply to inventory
        var inv = await _db.Inventories
            .FirstOrDefaultAsync(i => i.ProductId == adj.ProductId && i.LocationId == adj.LocationId);

        if (inv == null)
        {
            inv = new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = adj.ProductId,
                LocationId = adj.LocationId,
                QtyOnHand = adj.QtyAfter,
                QtyReserved = 0,
                UnitCost = 0,
                Status = InventoryStatus.Available
            };
            _db.Inventories.Add(inv);
        }
        else
        {
            inv.QtyOnHand = adj.QtyAfter;
        }

        await _db.SaveChangesAsync();

        await _db.Entry(adj).Reference(a => a.Approver).LoadAsync();
        return _mapper.Map<AdjustmentResponseDto>(adj);
    }
}
