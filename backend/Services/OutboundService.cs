using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class OutboundService : IOutboundService
{
    private readonly WmsDbContext _db;
    private readonly IMapper _mapper;

    public OutboundService(WmsDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    private async Task<string> GenerateOrderNo()
    {
        var today = DateTime.UtcNow.Date;
        var count = await _db.OutboundOrders
            .Where(o => o.CreatedAt >= today && o.CreatedAt < today.AddDays(1))
            .CountAsync() + 1;
        return $"ORD-{today:yyyyMMdd}-{count:D4}";
    }

    public async Task<IEnumerable<OutboundOrderResponseDto>> GetAllAsync()
    {
        var orders = await _db.OutboundOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Creator)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return _mapper.Map<IEnumerable<OutboundOrderResponseDto>>(orders);
    }

    public async Task<OutboundOrderResponseDto?> GetByIdAsync(Guid id)
    {
        var order = await _db.OutboundOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Creator)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.PackLists)
            .Include(o => o.Shipments).ThenInclude(s => s.Carrier)
            .FirstOrDefaultAsync(o => o.Id == id);
        return order == null ? null : _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<OutboundOrderResponseDto> CreateAsync(CreateOutboundOrderDto dto, Guid createdBy)
    {
        var order = new OutboundOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = await GenerateOrderNo(),
            WarehouseId = dto.WarehouseId,
            OrderType = dto.OrderType,
            Status = OrderStatus.Draft,
            Priority = dto.Priority,
            Destination = dto.Destination,
            RequestedDate = dto.RequestedDate,
            DueDate = dto.DueDate,
            CreatedBy = createdBy,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.OutboundOrders.Add(order);
        await _db.SaveChangesAsync();

        await _db.Entry(order).Reference(o => o.Warehouse).LoadAsync();
        await _db.Entry(order).Reference(o => o.Creator).LoadAsync();

        return _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<OutboundOrderResponseDto?> UpdateAsync(Guid id, UpdateOutboundOrderDto dto)
    {
        var order = await _db.OutboundOrders.FindAsync(id);
        if (order == null) return null;

        if (dto.Priority.HasValue) order.Priority = dto.Priority.Value;
        if (dto.Destination != null) order.Destination = dto.Destination;
        if (dto.DueDate.HasValue) order.DueDate = dto.DueDate;
        if (dto.Notes != null) order.Notes = dto.Notes;
        if (dto.Status.HasValue) order.Status = dto.Status.Value;

        await _db.SaveChangesAsync();
        await _db.Entry(order).Reference(o => o.Warehouse).LoadAsync();
        await _db.Entry(order).Reference(o => o.Creator).LoadAsync();

        return _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var order = await _db.OutboundOrders.FindAsync(id);
        if (order == null || order.Status != OrderStatus.Draft) return false;
        _db.OutboundOrders.Remove(order);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OrderItemResponseDto> AddItemAsync(Guid orderId, AddOrderItemDto dto)
    {
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = dto.ProductId,
            OrderedQty = dto.OrderedQty,
            PickedQty = 0,
            ShippedQty = 0,
            UnitPrice = dto.UnitPrice,
            Status = OrderItemStatus.Pending
        };

        _db.OrderItems.Add(item);
        await _db.SaveChangesAsync();

        await _db.Entry(item).Reference(i => i.Product).LoadAsync();
        return _mapper.Map<OrderItemResponseDto>(item);
    }

    public async Task<IEnumerable<OrderItemResponseDto>> GetOrderItemsAsync(Guid orderId)
    {
        var items = await _db.OrderItems
            .Include(i => i.Product)
            .Where(i => i.OrderId == orderId)
            .ToListAsync();
        return _mapper.Map<IEnumerable<OrderItemResponseDto>>(items);
    }

    public async Task<OutboundOrderResponseDto?> StartPickingAsync(Guid orderId)
    {
        var order = await _db.OutboundOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Creator)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.Status != OrderStatus.Draft) return null;

        // Create pick tasks for each item
        foreach (var orderItem in order.Items)
        {
            // Find inventory locations for this product
            var invItems = await _db.Inventories
                .Where(i => i.ProductId == orderItem.ProductId && i.QtyOnHand > 0 && i.Status == InventoryStatus.Available)
                .OrderByDescending(i => i.QtyOnHand)
                .Take(3)
                .ToListAsync();

            decimal remaining = orderItem.OrderedQty;
            foreach (var inv in invItems)
            {
                if (remaining <= 0) break;
                var qty = Math.Min(remaining, inv.QtyOnHand);

                var pickTask = new PickTask
                {
                    Id = Guid.NewGuid(),
                    OrderItemId = orderItem.Id,
                    ProductId = orderItem.ProductId,
                    FromLocationId = inv.LocationId,
                    QtyToPick = qty,
                    QtyPicked = 0,
                    Status = PickTaskStatus.Assigned,
                    AssignedAt = DateTime.UtcNow
                };

                _db.PickTasks.Add(pickTask);
                inv.QtyReserved += qty;
                remaining -= qty;
            }

            orderItem.Status = OrderItemStatus.Picked;
            orderItem.PickedQty = orderItem.OrderedQty - remaining;
        }

        order.Status = OrderStatus.Picked;
        await _db.SaveChangesAsync();

        await _db.Entry(order).Reference(o => o.Warehouse).LoadAsync();
        await _db.Entry(order).Reference(o => o.Creator).LoadAsync();

        return _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<OutboundOrderResponseDto?> AssignPickerAsync(Guid orderId, AssignPickerDto dto)
    {
        var order = await _db.OutboundOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Creator)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null) return null;

        var tasks = await _db.PickTasks.Where(t => t.OrderItem!.OrderId == orderId).ToListAsync();
        foreach (var task in tasks)
        {
            task.PickerId = dto.PickerId;
            task.Status = PickTaskStatus.Assigned;
            task.AssignedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _db.Entry(order).Reference(o => o.Warehouse).LoadAsync();
        await _db.Entry(order).Reference(o => o.Creator).LoadAsync();

        return _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<OutboundOrderResponseDto?> PackOrderAsync(Guid orderId, PackOrderDto dto, Guid userId)
    {
        var order = await _db.OutboundOrders
            .Include(o => o.Warehouse)
            .Include(o => o.Creator)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.Status != OrderStatus.Picked) return null;

        var packList = new PackList
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CartonNo = dto.CartonNo,
            PackedBy = userId,
            WeightKg = dto.WeightKg,
            PackedAt = DateTime.UtcNow,
            Status = PackListStatus.Open
        };

        _db.PackLists.Add(packList);
        order.Status = OrderStatus.Packed;

        await _db.SaveChangesAsync();
        await _db.Entry(order).Reference(o => o.Warehouse).LoadAsync();
        await _db.Entry(order).Reference(o => o.Creator).LoadAsync();

        return _mapper.Map<OutboundOrderResponseDto>(order);
    }

    public async Task<ShipmentResponseDto> ShipOrderAsync(Guid orderId, CreateShipmentDto dto, Guid userId)
    {
        var order = await _db.OutboundOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found");

        if (order.Status != OrderStatus.Packed)
            throw new InvalidOperationException("Only packed orders can be shipped");

        // Deduct reserved stock for every pick task of this order
        var tasks = await _db.PickTasks
            .Where(t => t.OrderItem!.OrderId == orderId)
            .ToListAsync();

        foreach (var task in tasks)
        {
            var inv = await _db.Inventories.FirstOrDefaultAsync(i =>
                i.ProductId == task.ProductId && i.LocationId == task.FromLocationId);
            if (inv == null) continue;

            var before = inv.QtyOnHand;
            inv.QtyOnHand -= task.QtyToPick;
            inv.QtyReserved = Math.Max(0, inv.QtyReserved - task.QtyToPick);
            task.QtyPicked = task.QtyToPick;
            task.Status = PickTaskStatus.Picked;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = task.ProductId,
                LocationId = task.FromLocationId,
                ReferenceType = ReferenceType.Outbound,
                ReferenceId = orderId,
                TransactionType = TransactionType.Out,
                QtyBefore = before,
                QtyAfter = inv.QtyOnHand,
                UomQty = task.QtyToPick,
                UserId = userId,
                LotNumber = inv.LotNumber,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var item in order.Items)
        {
            item.ShippedQty = item.PickedQty;
            item.Status = OrderItemStatus.Shipped;
        }

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CarrierId = dto.CarrierId,
            TrackingNo = dto.TrackingNo,
            BolNumber = dto.BolNumber,
            VehiclePlate = dto.VehiclePlate,
            DriverName = dto.DriverName,
            Status = ShipmentStatus.Dispatched,
            ShippedAt = DateTime.UtcNow
        };

        order.Status = OrderStatus.Shipped;
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        await _db.Entry(shipment).Reference(s => s.Order).LoadAsync();
        await _db.Entry(shipment).Reference(s => s.Carrier).LoadAsync();

        return _mapper.Map<ShipmentResponseDto>(shipment);
    }
}
