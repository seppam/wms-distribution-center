using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Transfer;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class TransferService : ITransferService
{
    private readonly WmsDbContext _db;

    public TransferService(WmsDbContext db) => _db = db;

    private IQueryable<Transfer> WithRefs() => _db.Transfers
        .Include(t => t.Product).Include(t => t.FromLocation).Include(t => t.ToLocation).Include(t => t.Requester);

    public async Task<IEnumerable<TransferResponseDto>> GetAllAsync()
    {
        var rows = await WithRefs().ToListAsync();
        return rows.OrderByDescending(t => t.TransferNo).Select(t => t.ToDto()).ToList();
    }

    public async Task<TransferResponseDto?> GetByIdAsync(Guid id) => (await WithRefs().FirstOrDefaultAsync(t => t.Id == id))?.ToDto();

    public async Task<TransferResponseDto> CreateAsync(CreateTransferDto dto, Guid userId)
    {
        if (dto.FromLocationId == dto.ToLocationId)
            throw new BusinessRuleException("Source and destination locations must be different.");
        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId && p.IsActive))
            throw new BusinessRuleException("Product not found or inactive.");
        await EnsureLocationUsableAsync(dto.ToLocationId, "Destination");
        await EnsureLocationUsableAsync(dto.FromLocationId, "Source");

        var source = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == dto.ProductId && i.LocationId == dto.FromLocationId);
        var available = source == null ? 0 : StockHelper.Available(source);
        if (available < dto.Qty)
            throw new BusinessRuleException($"Only {available:0.##} available at the source location (reserved stock cannot be moved).");

        var today = DateTime.UtcNow.Date;
        var seq = await _db.Transfers.CountAsync(t => t.TransferNo.StartsWith("TRF-" + today.ToString("yyyyMMdd")));
        var transfer = new Transfer
        {
            Id = Guid.NewGuid(), TransferNo = StockHelper.NextNumber("TRF", seq), ProductId = dto.ProductId,
            FromLocationId = dto.FromLocationId, ToLocationId = dto.ToLocationId, Qty = dto.Qty, Reason = dto.Reason,
            RequestedBy = userId, Status = TransferStatus.Draft
        };
        _db.Transfers.Add(transfer);
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(transfer.Id))!;
    }

    private async Task EnsureLocationUsableAsync(Guid id, string role)
    {
        var loc = await _db.Locations.FindAsync(id) ?? throw new BusinessRuleException($"{role} location not found.");
        if (loc.Status == LocationStatus.Locked) throw new BusinessRuleException($"{role} location {loc.LocationCode} is locked.");
    }

    public async Task<TransferResponseDto?> CompleteAsync(Guid id, Guid userId)
    {
        var transfer = await _db.Transfers.FirstOrDefaultAsync(t => t.Id == id);
        if (transfer == null) return null;
        if (transfer.Status != TransferStatus.Draft)
            throw new BusinessRuleException($"Only draft transfers can be completed (this one is {transfer.Status}).");
        await EnsureLocationUsableAsync(transfer.ToLocationId, "Destination");
        await EnsureLocationUsableAsync(transfer.FromLocationId, "Source");

        var source = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == transfer.ProductId && i.LocationId == transfer.FromLocationId);
        if (source == null || StockHelper.Available(source) < transfer.Qty)
            throw new BusinessRuleException(
                $"Only {(source == null ? 0 : StockHelper.Available(source)):0.##} is available at the source now; the transfer cannot be completed.");

        var target = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == transfer.ProductId && i.LocationId == transfer.ToLocationId);
        if (target != null && target.QtyOnHand > 0 && !string.Equals(target.LotNumber, source.LotNumber, StringComparison.Ordinal))
            throw new BusinessRuleException("The destination already holds another lot of this product.");

        var sourceBefore = source.QtyOnHand;
        source.QtyOnHand -= transfer.Qty;
        if (target == null)
        {
            target = new Inventory
            {
                Id = Guid.NewGuid(), ProductId = transfer.ProductId, LocationId = transfer.ToLocationId, QtyOnHand = 0,
                LotNumber = source.LotNumber, ExpiryDate = source.ExpiryDate, UnitCost = source.UnitCost, Status = source.Status
            };
            _db.Inventories.Add(target);
        }
        var targetBefore = target.QtyOnHand;
        target.QtyOnHand += transfer.Qty;

        StockHelper.LogTransaction(_db, transfer.ProductId, transfer.FromLocationId, ReferenceType.Transfer, transfer.Id,
            TransactionType.Out, sourceBefore, source.QtyOnHand, transfer.Qty, userId, source.LotNumber, transfer.TransferNo);
        StockHelper.LogTransaction(_db, transfer.ProductId, transfer.ToLocationId, ReferenceType.Transfer, transfer.Id,
            TransactionType.In, targetBefore, target.QtyOnHand, transfer.Qty, userId, source.LotNumber, transfer.TransferNo);

        transfer.Status = TransferStatus.Completed;
        transfer.TransferredAt = DateTime.UtcNow;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Stock changed while transferring. Please retry."); }
        await StockHelper.SyncLocationStatusAsync(_db, transfer.FromLocationId, transfer.ToLocationId);
        return await GetByIdAsync(id);
    }

    public async Task<TransferResponseDto?> CancelAsync(Guid id)
    {
        var transfer = await _db.Transfers.FindAsync(id);
        if (transfer == null) return null;
        if (transfer.Status != TransferStatus.Draft)
            throw new BusinessRuleException($"Only draft transfers can be cancelled (this one is {transfer.Status}).");
        transfer.Status = TransferStatus.Cancelled;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }
}
