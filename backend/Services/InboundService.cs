using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class InboundService : IInboundService
{
    private readonly WmsDbContext _db;

    public InboundService(WmsDbContext db)
    {
        _db = db;
    }

    private async Task<string> GenerateReceiptNo()
    {
        var today = DateTime.UtcNow.Date;
        var count = await _db.InboundReceipts
            .Where(r => r.CreatedAt >= today && r.CreatedAt < today.AddDays(1))
            .CountAsync() + 1;
        return $"RCV-{today:yyyyMMdd}-{count:D4}";
    }

    public async Task<IEnumerable<InboundReceiptResponseDto>> GetAllAsync()
    {
        var receipts = await _db.InboundReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.Receiver)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return receipts.Select(x => x.ToDto()).ToList();
    }

    public async Task<InboundReceiptResponseDto?> GetByIdAsync(Guid id)
    {
        var receipt = await _db.InboundReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.Receiver)
            .Include(r => r.Items).ThenInclude(i => i.Product)
            .Include(r => r.Items).ThenInclude(i => i.PutawayLocation)
            .FirstOrDefaultAsync(r => r.Id == id);
        return receipt?.ToDto();
    }

    public async Task<InboundReceiptResponseDto> CreateAsync(CreateInboundReceiptDto dto)
    {
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
        await _db.SaveChangesAsync();

        await _db.Entry(receipt).Reference(r => r.Supplier).LoadAsync();
        await _db.Entry(receipt).Reference(r => r.Warehouse).LoadAsync();

        return receipt.ToDto();
    }

    public async Task<InboundReceiptResponseDto?> UpdateAsync(Guid id, UpdateInboundReceiptDto dto)
    {
        var receipt = await _db.InboundReceipts.FindAsync(id);
        if (receipt == null) return null;

        if (dto.PoNumber != null) receipt.PoNumber = dto.PoNumber;
        if (dto.AsnNumber != null) receipt.AsnNumber = dto.AsnNumber;
        if (dto.ExpectedDate.HasValue) receipt.ExpectedDate = dto.ExpectedDate;
        if (dto.DockDoor != null) receipt.DockDoor = dto.DockDoor;
        if (dto.Notes != null) receipt.Notes = dto.Notes;
        if (dto.Status.HasValue) receipt.Status = dto.Status.Value;

        await _db.SaveChangesAsync();
        await _db.Entry(receipt).Reference(r => r.Supplier).LoadAsync();
        await _db.Entry(receipt).Reference(r => r.Warehouse).LoadAsync();
        await _db.Entry(receipt).Reference(r => r.Receiver).LoadAsync();

        return receipt.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var receipt = await _db.InboundReceipts.FindAsync(id);
        if (receipt == null || receipt.Status != InboundStatus.Draft) return false;
        _db.InboundReceipts.Remove(receipt);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ReceiptItemResponseDto> AddItemAsync(Guid receiptId, AddReceiptItemDto dto)
    {
        var item = new ReceiptItem
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

        var item = await _db.ReceiptItems
            .Include(i => i.Product)
            .Include(i => i.PutawayLocation)
            .FirstOrDefaultAsync(i => i.Id == dto.ReceiptItemId && i.ReceiptId == receiptId);

        if (item == null) return null;

        // Receiving can be repeated for the same line: only post the delta to stock
        var previousReceivedQty = item.ReceivedQty;
        var deltaQty = dto.ReceivedQty - previousReceivedQty;
        item.ReceivedQty = dto.ReceivedQty;
        item.DamagedQty = dto.DamagedQty;
        item.PutawayLocationId = dto.PutawayLocationId ?? item.PutawayLocationId;
        if (dto.LotNumber != null) item.LotNumber = dto.LotNumber;
        if (dto.ExpiryDate.HasValue) item.ExpiryDate = dto.ExpiryDate.Value;

        // Determine status
        if (dto.DamagedQty > 0)
            item.Status = ReceiptItemStatus.Damaged;
        else if (item.ReceivedQty < item.ExpectedQty)
            item.Status = ReceiptItemStatus.Shortage;
        else
            item.Status = ReceiptItemStatus.Received;

        // Update inventory
        var locationId = item.PutawayLocationId ?? (await _db.Locations.FirstOrDefaultAsync(l => l.Zone.WarehouseId == receipt.WarehouseId && l.Status == Enums.LocationStatus.Empty))?.Id;
        if (locationId.HasValue)
        {
            var inv = await _db.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.LocationId == locationId.Value);

            if (inv == null)
            {
                inv = new Inventory
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    LocationId = locationId.Value,
                    QtyOnHand = deltaQty,
                    QtyReserved = 0,
                    LotNumber = item.LotNumber,
                    ExpiryDate = item.ExpiryDate,
                    UnitCost = item.UnitCost,
                    Status = InventoryStatus.Available
                };
                _db.Inventories.Add(inv);
            }
            else
            {
                inv.QtyOnHand += deltaQty;
            }

            // Putaway: the location now holds stock
            var putawayLocation = await _db.Locations.FindAsync(locationId.Value);
            if (putawayLocation != null && putawayLocation.Status == Enums.LocationStatus.Empty)
                putawayLocation.Status = Enums.LocationStatus.Occupied;

            // Log transaction
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = item.ProductId,
                LocationId = locationId.Value,
                ReferenceType = ReferenceType.Inbound,
                ReferenceId = receiptId,
                TransactionType = TransactionType.In,
                QtyBefore = inv.QtyOnHand - deltaQty,
                QtyAfter = inv.QtyOnHand,
                UomQty = deltaQty,
                UserId = userId,
                LotNumber = item.LotNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        receipt.Status = InboundStatus.InProgress;
        receipt.ReceivedBy = userId;
        receipt.ReceivedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return item.ToDto();
    }

    public async Task<InboundReceiptResponseDto?> CompleteReceiptAsync(Guid receiptId, Guid userId)
    {
        var receipt = await _db.InboundReceipts
            .Include(r => r.Supplier)
            .Include(r => r.Warehouse)
            .Include(r => r.Receiver)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == receiptId);

        if (receipt == null) return null;

        var allReceived = receipt.Items.All(i => i.Status == ReceiptItemStatus.Received);
        var anyDamaged = receipt.Items.Any(i => i.Status == ReceiptItemStatus.Damaged);
        var anyShortage = receipt.Items.Any(i => i.Status == ReceiptItemStatus.Shortage);

        receipt.Status = allReceived ? InboundStatus.Received :
                         anyShortage ? InboundStatus.Partial : InboundStatus.InProgress;

        await _db.SaveChangesAsync();
        return receipt.ToDto();
    }
}
