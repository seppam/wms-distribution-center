using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

/// <summary>Shared stock bookkeeping used by the workflow services.</summary>
internal static class StockHelper
{
    public static decimal Available(Inventory inv) => inv.QtyOnHand - inv.QtyReserved;

    /// <summary>Stages an immutable ledger entry for a quantity change; call before SaveChanges.</summary>
    public static void LogTransaction(WmsDbContext db, Guid productId, Guid locationId, ReferenceType refType, Guid refId,
        TransactionType type, decimal qtyBefore, decimal qtyAfter, decimal movedQty, Guid userId, string? lot, string? notes = null) =>
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = productId, LocationId = locationId, ReferenceType = refType, ReferenceId = refId,
            TransactionType = type, QtyBefore = qtyBefore, QtyAfter = qtyAfter, UomQty = movedQty,
            UserId = userId, LotNumber = lot, Notes = notes, CreatedAt = DateTime.UtcNow
        });

    /// <summary>Marks locations Occupied when they hold stock and Empty when they do not (Locked locations are left alone).</summary>
    public static async Task SyncLocationStatusAsync(WmsDbContext db, params Guid[] locationIds)
    {
        foreach (var id in locationIds.Distinct())
        {
            var location = await db.Locations.FindAsync(id);
            if (location == null || location.Status == LocationStatus.Locked) continue;
            var hasStock = await db.Inventories.AnyAsync(i => i.LocationId == id && i.QtyOnHand > 0);
            var target = hasStock ? LocationStatus.Occupied : LocationStatus.Empty;
            if (location.Status != target) location.Status = target;
        }
        await db.SaveChangesAsync();
    }

    public static string NextNumber(string prefix, int todaysCount) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{todaysCount + 1:D4}";
}
