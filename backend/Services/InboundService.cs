using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class InboundService : IInboundService
{
    private readonly WmsDbContext _db;

    public InboundService(WmsDbContext db) => _db = db;

    private async Task<string> GenerateReceiptNo()
    {
        var today = DateTime.UtcNow.Date;
        var count = await _db.InboundReceipts.CountAsync(r => r.CreatedAt >= today && r.CreatedAt < today.AddDays(1));
        return StockHelper.NextNumber("RCV", count);
    }

    private IQueryable<InboundReceipt> WithHeader() => _db.InboundReceipts
        .Include(r => r.Supplier).Include(r => r.Warehouse).Include(r => r.Receiver);

    public async Task<IEnumerable<InboundReceiptResponseDto>> GetAllAsync()
    {
        var receipts = await WithHeader().OrderByDescending(r => r.CreatedAt).ToListAsync();
        return receipts.Select(r => r.ToDto()).ToList();
    }

    public async Task<InboundReceiptResponseDto?> GetByIdAsync(Guid id)
    {
        var receipt = await WithHeader()
            .Include(r => r.Items).ThenInclude(i => i.Product)
            .Include(r => r.Items).ThenInclude(i => i.PutawayLocation)
            .FirstOrDefaultAsync(r => r.Id == id);
        return receipt?.ToDetailDto();
    }

    public async Task<InboundReceiptResponseDto> CreateAsync(CreateInboundReceiptDto dto)
    {
        if (!await _db.Suppliers.AnyAsync(s => s.Id == dto.SupplierId && s.IsActive))
            throw new BusinessRuleException("Supplier not found or inactive.");
        if (!await _db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.IsActive))
            throw new BusinessRuleException("Warehouse not found or inactive.");

        var receipt = new InboundReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = await GenerateReceiptNo(),
            SupplierId = dto.SupplierId,
            WarehouseId = dto.WarehouseId,
            PoNumber = dto.PoNumber,
            AsnNumber = dto.AsnNumber,
            Status = InboundStatus.Draft,
            ExpectedDate = dto.ExpectedDate,
            DockDoor = dto.DockDoor,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.InboundReceipts.Add(receipt);

        foreach (var line in dto.Items ?? [])
            receipt.Items.Add(await BuildItemAsync(receipt.Id, line, receipt.Items.Select(i => i.ProductId)));

        await _db.SaveChangesAsync();
        return (await GetByIdAsync(receipt.Id))!;
    }

    public async Task<InboundReceiptResponseDto?> UpdateAsync(Guid id, UpdateInboundReceiptDto dto)
    {
        var receipt = await _db.InboundReceipts.FindAsync(id);
        if (receipt == null) return null;
        if (receipt.Status != InboundStatus.Draft)
            throw new BusinessRuleException("Only draft receipts can be edited.");

        if (dto.PoNumber != null) receipt.PoNumber = dto.PoNumber;
        if (dto.AsnNumber != null) receipt.AsnNumber = dto.AsnNumber;
        if (dto.ExpectedDate.HasValue) receipt.ExpectedDate = dto.ExpectedDate;
        if (dto.DockDoor != null) receipt.DockDoor = dto.DockDoor;
        if (dto.Notes != null) receipt.Notes = dto.Notes;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var receipt = await _db.InboundReceipts.FindAsync(id);
        if (receipt == null) return false;
        if (receipt.Status != InboundStatus.Draft)
            throw new BusinessRuleException("Only draft receipts can be deleted.");
        _db.InboundReceipts.Remove(receipt);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<ReceiptItem> BuildItemAsync(Guid receiptId, AddReceiptItemDto dto, IEnumerable<Guid> existingProducts)
    {
        if (existingProducts.Contains(dto.ProductId))
            throw new BusinessRuleException("This product is already on the receipt.");
        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId && p.IsActive))
            throw new BusinessRuleException("Product not found or inactive.");
        if (dto.PutawayLocationId.HasValue && !await _db.Locations.AnyAsync(l => l.Id == dto.PutawayLocationId))
            throw new BusinessRuleException("Put-away location not found.");

        return new ReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            ProductId = dto.ProductId,
            ExpectedQty = dto.ExpectedQty,
            ReceivedQty = 0,
            DamagedQty = 0,
            LotNumber = dto.LotNumber,
            ExpiryDate = dto.ExpiryDate,
            UnitCost = dto.UnitCost,
            PutawayLocationId = dto.PutawayLocationId,
            Status = ReceiptItemStatus.Pending
        };
    }

    public async Task<ReceiptItemResponseDto> AddItemAsync(Guid receiptId, AddReceiptItemDto dto)
    {
        var receipt = await _db.InboundReceipts.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == receiptId)
            ?? throw new NotFoundException("Receipt not found.");
        if (receipt.Status is not (InboundStatus.Draft or InboundStatus.InProgress))
            throw new BusinessRuleException("Lines can only be added to open receipts.");

        var item = await BuildItemAsync(receiptId, dto, receipt.Items.Select(i => i.ProductId));
        _db.ReceiptItems.Add(item);
        await _db.SaveChangesAsync();

        await _db.Entry(item).Reference(i => i.Product).LoadAsync();
        await _db.Entry(item).Reference(i => i.PutawayLocation).LoadAsync();
        return item.ToDto();
    }

    public async Task<ReceiptItemResponseDto?> ReceiveItemAsync(Guid receiptId, ReceiveItemDto dto, Guid userId)
    {
        var receipt = await _db.InboundReceipts.FindAsync(receiptId);
        if (receipt == null) return null;
        if (receipt.Status is InboundStatus.Received or InboundStatus.Cancelled)
            throw new BusinessRuleException($"A {receipt.Status} receipt cannot receive more goods.");

        var item = await _db.ReceiptItems
            .Include(i => i.Product)
            .Include(i => i.PutawayLocation)
            .FirstOrDefaultAsync(i => i.Id == dto.ReceiptItemId && i.ReceiptId == receiptId);
        if (item == null) return null;

        if (dto.DamagedQty > dto.ReceivedQty)
            throw new BusinessRuleException("Damaged quantity cannot exceed the received quantity.");

        // Put-away: the good quantity is stocked at the chosen location in the same transaction.
        var locationId = dto.PutawayLocationId ?? item.PutawayLocationId
            ?? throw new BusinessRuleException("Choose a put-away location.");
        var location = await _db.Locations.Include(l => l.Zone).FirstOrDefaultAsync(l => l.Id == locationId)
            ?? throw new BusinessRuleException("Put-away location not found.");
        if (location.Zone.WarehouseId != receipt.WarehouseId)
            throw new BusinessRuleException("The put-away location belongs to a different warehouse.");
        if (location.Status == LocationStatus.Locked)
            throw new BusinessRuleException("The put-away location is locked.");

        // Receiving can be repeated for the same line: only the change in good quantity is posted to stock.
        var previousGood = item.ReceivedQty - item.DamagedQty;
        var newGood = dto.ReceivedQty - dto.DamagedQty;
        var delta = newGood - previousGood;

        var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.LocationId == locationId);
        if (item.PutawayLocationId.HasValue && item.PutawayLocationId != locationId && previousGood > 0)
            throw new BusinessRuleException("Stock from this line was already put away to another location; keep the same location.");

        var lot = dto.LotNumber ?? item.LotNumber;
        if (inv != null && delta > 0 && !string.Equals(inv.LotNumber, lot, StringComparison.Ordinal) && inv.QtyOnHand > 0)
            throw new BusinessRuleException("This location already holds another lot of the product; choose a different location.");
        if (inv != null && inv.QtyOnHand + delta < inv.QtyReserved)
            throw new BusinessRuleException("Reducing the received quantity would drop stock below what is reserved for orders.");
        if (inv == null && delta < 0)
            throw new BusinessRuleException("There is no stock at this location to reduce.");

        item.ReceivedQty = dto.ReceivedQty;
        item.DamagedQty = dto.DamagedQty;
        item.PutawayLocationId = locationId;
        if (dto.LotNumber != null) item.LotNumber = dto.LotNumber;
        if (dto.ExpiryDate.HasValue) item.ExpiryDate = dto.ExpiryDate.Value;
        item.Status = dto.DamagedQty > 0 ? ReceiptItemStatus.Damaged
            : item.ReceivedQty < item.ExpectedQty ? ReceiptItemStatus.Shortage
            : ReceiptItemStatus.Received;

        if (delta != 0)
        {
            if (inv == null)
            {
                inv = new Inventory
                {
                    Id = Guid.NewGuid(), ProductId = item.ProductId, LocationId = locationId, QtyOnHand = 0, QtyReserved = 0,
                    LotNumber = item.LotNumber, ExpiryDate = item.ExpiryDate, UnitCost = item.UnitCost, Status = InventoryStatus.Available
                };
                _db.Inventories.Add(inv);
            }
            var before = inv.QtyOnHand;
            inv.QtyOnHand += delta;
            StockHelper.LogTransaction(_db, item.ProductId, locationId, ReferenceType.Inbound, receiptId,
                TransactionType.In, before, inv.QtyOnHand, delta, userId, item.LotNumber);
        }

        receipt.Status = InboundStatus.InProgress;
        receipt.ReceivedBy = userId;
        receipt.ReceivedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await StockHelper.SyncLocationStatusAsync(_db, locationId);
        await _db.Entry(item).Reference(i => i.PutawayLocation).LoadAsync();
        return item.ToDto();
    }

    public async Task<InboundReceiptResponseDto?> CompleteReceiptAsync(Guid receiptId, Guid userId)
    {
        var receipt = await _db.InboundReceipts.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null) return null;
        if (receipt.Status is InboundStatus.Received or InboundStatus.Partial or InboundStatus.Cancelled)
            throw new BusinessRuleException($"The receipt is already {receipt.Status}.");
        if (receipt.Items.Count == 0)
            throw new BusinessRuleException("A receipt without lines cannot be completed.");
        if (receipt.Items.All(i => i.Status == ReceiptItemStatus.Pending))
            throw new BusinessRuleException("Receive at least one line before completing the receipt.");

        // Fully matching delivery -> Received; any shortage, damage or untouched line -> Partial
        receipt.Status = receipt.Items.All(i => i.Status == ReceiptItemStatus.Received)
            ? InboundStatus.Received
            : InboundStatus.Partial;
        receipt.ReceivedBy = userId;
        receipt.ReceivedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(receiptId);
    }
}
