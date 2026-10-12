using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Report;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Services;

public class ReportService : IReportService
{
    private readonly WmsDbContext _db;

    public ReportService(WmsDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var weekFromNow = today.AddDays(7);

        var totalProducts = await _db.Products.CountAsync(p => p.IsActive);
        var totalLocations = await _db.Locations.CountAsync();
        var totalInboundToday = await _db.InboundReceipts.CountAsync(r => r.CreatedAt >= today && r.CreatedAt < tomorrow);
        var totalOutboundToday = await _db.OutboundOrders.CountAsync(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);

        var totalInventoryValue = await _db.Inventories
            .SumAsync(i => i.QtyOnHand * i.UnitCost);

        var lowStockCount = await _db.Products
            .Where(p => p.IsActive)
            .Where(p => p.Inventories.Sum(i => i.QtyOnHand) < p.MinStock)
            .CountAsync();

        var expiringSoonCount = await _db.Inventories
            .Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= DateOnly.FromDateTime(weekFromNow) && i.Status != InventoryStatus.Damaged)
            .CountAsync();

        return new DashboardDto(
            totalProducts,
            totalLocations,
            totalInboundToday,
            totalOutboundToday,
            totalInventoryValue,
            lowStockCount,
            expiringSoonCount
        );
    }

    public async Task<IEnumerable<InventorySummaryDto>> GetInventorySummaryAsync()
    {
        var data = await _db.Inventories
            .Include(i => i.Product)
            .GroupBy(i => new { i.ProductId, i.Product!.Name, i.Product.Sku })
            .Select(g => new InventorySummaryDto(
                g.Key.ProductId,
                g.Key.Name,
                g.Key.Sku,
                g.Sum(x => x.QtyOnHand),
                g.Sum(x => x.QtyReserved),
                g.Sum(x => x.QtyOnHand - x.QtyReserved),
                g.Count()
            ))
            .ToListAsync();

        return data;
    }

    public async Task<InboundSummaryDto> GetInboundSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var receipts = await _db.InboundReceipts
            .Where(r => r.CreatedAt >= today.AddDays(-30))
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return new InboundSummaryDto(
            receipts.FirstOrDefault(r => r.Status == InboundStatus.Draft)?.Count ?? 0,
            receipts.FirstOrDefault(r => r.Status == InboundStatus.InProgress)?.Count ?? 0,
            receipts.FirstOrDefault(r => r.Status == InboundStatus.Received)?.Count ?? 0,
            receipts.FirstOrDefault(r => r.Status == InboundStatus.Partial)?.Count ?? 0,
            receipts.FirstOrDefault(r => r.Status == InboundStatus.Cancelled)?.Count ?? 0
        );
    }

    public async Task<OutboundSummaryDto> GetOutboundSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var orders = await _db.OutboundOrders
            .Where(o => o.CreatedAt >= today.AddDays(-30))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return new OutboundSummaryDto(
            orders.FirstOrDefault(r => r.Status == OrderStatus.Draft)?.Count ?? 0,
            orders.FirstOrDefault(r => r.Status == OrderStatus.Picked)?.Count ?? 0,
            orders.FirstOrDefault(r => r.Status == OrderStatus.Packed)?.Count ?? 0,
            orders.FirstOrDefault(r => r.Status == OrderStatus.Shipped)?.Count ?? 0,
            orders.FirstOrDefault(r => r.Status == OrderStatus.Completed)?.Count ?? 0,
            orders.FirstOrDefault(r => r.Status == OrderStatus.Cancelled)?.Count ?? 0
        );
    }
}
