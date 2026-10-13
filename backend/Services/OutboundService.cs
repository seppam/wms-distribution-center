using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Security;

namespace WmsDistributionCenter.Api.Services;

public class OutboundService : IOutboundService
{
    private readonly WmsDbContext _db;

    public OutboundService(WmsDbContext db) => _db = db;

    private async Task<string> GenerateOrderNo()
    {
        var today = DateTime.UtcNow.Date;
        var count = await _db.OutboundOrders.CountAsync(o => o.CreatedAt >= today && o.CreatedAt < today.AddDays(1));
        return StockHelper.NextNumber("ORD", count);
    }

    private IQueryable<OutboundOrder> WithHeader() => _db.OutboundOrders.Include(o => o.Warehouse).Include(o => o.Creator);

    public async Task<IEnumerable<OutboundOrderResponseDto>> GetAllAsync()
    {
        var orders = await WithHeader().OrderByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(o => o.ToDto()).ToList();
    }

    public async Task<OutboundOrderResponseDto?> GetByIdAsync(Guid id)
    {
        var order = await WithHeader()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.PackLists).ThenInclude(p => p.Packer)
            .Include(o => o.Shipments).ThenInclude(s => s.Carrier)
            .FirstOrDefaultAsync(o => o.Id == id);
        return order?.ToDetailDto();
    }

    public async Task<OutboundOrderResponseDto> CreateAsync(CreateOutboundOrderDto dto, Guid createdBy)
    {
        if (!await _db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.IsActive))
            throw new BusinessRuleException("Warehouse not found or inactive.");

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

        foreach (var line in dto.Items ?? [])
            order.Items.Add(await BuildItemAsync(order.Id, line, order.Items.Select(i => i.ProductId)));

        await _db.SaveChangesAsync();
        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<OutboundOrderResponseDto?> UpdateAsync(Guid id, UpdateOutboundOrderDto dto)
    {
        var order = await _db.OutboundOrders.FindAsync(id);
        if (order == null) return null;
        if (order.Status != OrderStatus.Draft)
            throw new BusinessRuleException("Only draft orders can be edited.");

        if (dto.Priority.HasValue) order.Priority = dto.Priority.Value;
        if (dto.Destination != null) order.Destination = dto.Destination;
        if (dto.DueDate.HasValue) order.DueDate = dto.DueDate;
        if (dto.Notes != null) order.Notes = dto.Notes;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var order = await _db.OutboundOrders.FindAsync(id);
        if (order == null) return false;
        if (order.Status != OrderStatus.Draft)
            throw new BusinessRuleException("Only draft orders can be deleted; cancel the order instead.");
        _db.OutboundOrders.Remove(order);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<OrderItem> BuildItemAsync(Guid orderId, AddOrderItemDto dto, IEnumerable<Guid> existingProducts)
    {
        if (existingProducts.Contains(dto.ProductId))
            throw new BusinessRuleException("This product is already on the order.");
        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId && p.IsActive))
            throw new BusinessRuleException("Product not found or inactive.");
        return new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = orderId, ProductId = dto.ProductId, OrderedQty = dto.OrderedQty,
            PickedQty = 0, ShippedQty = 0, UnitPrice = dto.UnitPrice, Status = OrderItemStatus.Pending
        };
    }

    public async Task<OrderItemResponseDto> AddItemAsync(Guid orderId, AddOrderItemDto dto)
    {
        var order = await _db.OutboundOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException("Order not found.");
        if (order.Status != OrderStatus.Draft)
            throw new BusinessRuleException("Lines can only be added to draft orders.");

        var item = await BuildItemAsync(orderId, dto, order.Items.Select(i => i.ProductId));
        _db.OrderItems.Add(item);
        await _db.SaveChangesAsync();
        await _db.Entry(item).Reference(i => i.Product).LoadAsync();
        return item.ToDto();
    }

    public async Task<IEnumerable<OrderItemResponseDto>> GetOrderItemsAsync(Guid orderId)
    {
        var items = await _db.OrderItems.Include(i => i.Product).Where(i => i.OrderId == orderId).ToListAsync();
        return items.Select(i => i.ToDto()).ToList();
    }

    // ---------------------------------------------------------------- picking

    public async Task<OutboundOrderResponseDto?> StartPickingAsync(Guid orderId)
    {
        var order = await _db.OutboundOrders.Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return null;
        if (order.Status != OrderStatus.Draft)
            throw new BusinessRuleException($"Only draft orders can start picking (this order is {order.Status}).");
        if (order.Items.Count == 0)
            throw new BusinessRuleException("The order has no lines.");

        // Allocate every line or none: plan all lines first, mutate only when every line can be covered.
        var plan = new List<(OrderItem Item, Inventory Inv, decimal Qty)>();
        foreach (var orderItem in order.Items)
        {
            var candidates = await _db.Inventories
                .Include(i => i.Location).ThenInclude(l => l.Zone)
                .Where(i => i.ProductId == orderItem.ProductId && i.Status == InventoryStatus.Available
                            && i.QtyOnHand - i.QtyReserved > 0 && i.Location.Zone.WarehouseId == order.WarehouseId)
                .ToListAsync();

            // First-expiry-first-out, then fullest bin first to limit the number of stops
            var ordered = candidates
                .OrderBy(i => i.ExpiryDate ?? DateOnly.MaxValue)
                .ThenByDescending(i => StockHelper.Available(i))
                .ToList();

            var totalAvailable = ordered.Sum(StockHelper.Available);
            if (totalAvailable < orderItem.OrderedQty)
                throw new BusinessRuleException(
                    $"Insufficient available stock for {orderItem.Product.Sku}: ordered {orderItem.OrderedQty:0.##}, available {totalAvailable:0.##}.");

            var remaining = orderItem.OrderedQty;
            foreach (var inv in ordered)
            {
                if (remaining <= 0) break;
                var qty = Math.Min(remaining, StockHelper.Available(inv));
                plan.Add((orderItem, inv, qty));
                remaining -= qty;
            }
        }

        foreach (var (item, inv, qty) in plan)
        {
            _db.PickTasks.Add(new PickTask
            {
                Id = Guid.NewGuid(), OrderItemId = item.Id, ProductId = item.ProductId,
                FromLocationId = inv.LocationId, QtyToPick = qty, QtyPicked = 0,
                Status = PickTaskStatus.Assigned, AssignedAt = DateTime.UtcNow
            });
            inv.QtyReserved += qty;
        }

        order.Status = OrderStatus.Picking;
        await SaveWithConflictMessage("Stock was allocated by another order at the same time. Please try again.");
        return await GetByIdAsync(orderId);
    }

    public async Task<OutboundOrderResponseDto?> AssignPickerAsync(Guid orderId, AssignPickerDto dto)
    {
        var order = await _db.OutboundOrders.FindAsync(orderId);
        if (order == null) return null;
        if (order.Status != OrderStatus.Picking)
            throw new BusinessRuleException("Pickers can only be assigned while the order is being picked.");

        var picker = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == dto.PickerId && u.IsActive)
            ?? throw new BusinessRuleException("Picker not found or inactive.");
        if (picker.Role.Name is not (AppRoles.Operator or AppRoles.Supervisor or AppRoles.Admin))
            throw new BusinessRuleException("The picker's role cannot perform floor work.");

        var tasks = await _db.PickTasks
            .Where(t => t.OrderItem.OrderId == orderId && t.Status != PickTaskStatus.Picked && t.Status != PickTaskStatus.Cancelled)
            .ToListAsync();
        foreach (var task in tasks)
        {
            task.PickerId = dto.PickerId;
            task.AssignedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return await GetByIdAsync(orderId);
    }

    public async Task<IEnumerable<PickTaskResponseDto>> GetPickTasksAsync(PickTaskStatus? status, Guid? orderId, Guid? pickerId)
    {
        var q = _db.PickTasks
            .Include(t => t.OrderItem).ThenInclude(i => i.Order)
            .Include(t => t.Product).Include(t => t.FromLocation).Include(t => t.Picker)
            .AsQueryable();
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        if (orderId.HasValue) q = q.Where(t => t.OrderItem.OrderId == orderId.Value);
        if (pickerId.HasValue) q = q.Where(t => t.PickerId == pickerId.Value);

        var tasks = await q.ToListAsync();
        return tasks
            .OrderBy(t => t.Status == PickTaskStatus.Picked || t.Status == PickTaskStatus.Cancelled ? 1 : 0)
            .ThenBy(t => t.OrderItem.Order.OrderNumber).ThenBy(t => t.FromLocation.LocationCode)
            .Select(t => t.ToDto()).ToList();
    }

    public async Task<PickTaskResponseDto?> ConfirmPickAsync(Guid taskId, ConfirmPickDto dto, Guid userId)
    {
        var task = await _db.PickTasks
            .Include(t => t.OrderItem).ThenInclude(i => i.Order)
            .Include(t => t.Product).Include(t => t.FromLocation).Include(t => t.Picker)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return null;

        var order = task.OrderItem.Order;
        if (order.Status != OrderStatus.Picking)
            throw new BusinessRuleException($"The order is {order.Status}; picks can only be confirmed while it is in Picking.");
        if (task.Status is PickTaskStatus.Picked or PickTaskStatus.Cancelled)
            throw new BusinessRuleException($"This pick task is already {task.Status}.");
        if (dto.QtyPicked < 0 || dto.QtyPicked > task.QtyToPick)
            throw new BusinessRuleException(
                $"Over-pick rejected: you can pick at most {task.QtyToPick:0.##} (requested {dto.QtyPicked:0.##}).");

        var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == task.ProductId && i.LocationId == task.FromLocationId)
            ?? throw new ConflictException("The stock record for this pick no longer exists.");
        if (inv.QtyOnHand < dto.QtyPicked)
            throw new BusinessRuleException("Not enough stock on hand at the pick location.");

        // A short pick releases the reservation for what was not found
        var shortfall = task.QtyToPick - dto.QtyPicked;
        inv.QtyReserved = Math.Max(0, inv.QtyReserved - shortfall);
        task.QtyPicked = dto.QtyPicked;
        task.Status = PickTaskStatus.Picked;
        task.PickedAt = DateTime.UtcNow;
        task.PickerId ??= userId;
        task.OrderItem.PickedQty += dto.QtyPicked;

        // When the last task of the order is done, the order is Picked
        var siblings = await _db.PickTasks
            .Where(t => t.OrderItem.OrderId == order.Id && t.Id != task.Id)
            .ToListAsync();
        if (siblings.All(t => t.Status is PickTaskStatus.Picked or PickTaskStatus.Cancelled))
        {
            var items = await _db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync();
            foreach (var item in items)
                item.Status = item.PickedQty >= item.OrderedQty ? OrderItemStatus.Picked : OrderItemStatus.Partial;
            order.Status = OrderStatus.Picked;
        }

        await SaveWithConflictMessage("The stock record changed while confirming the pick. Please retry.");
        await _db.Entry(task).Reference(t => t.Picker).LoadAsync();
        return task.ToDto();
    }

    // ---------------------------------------------------------------- pack / ship

    public async Task<OutboundOrderResponseDto?> PackOrderAsync(Guid orderId, PackOrderDto dto, Guid userId)
    {
        var order = await _db.OutboundOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return null;
        if (order.Status != OrderStatus.Picked)
            throw new BusinessRuleException($"Only picked orders can be packed (this order is {order.Status}).");
        if (order.Items.Sum(i => i.PickedQty) <= 0)
            throw new BusinessRuleException("Nothing was picked for this order; cancel it instead of packing.");

        _db.PackLists.Add(new PackList
        {
            Id = Guid.NewGuid(), OrderId = orderId, CartonNo = dto.CartonNo.Trim(), PackedBy = userId,
            WeightKg = dto.WeightKg, PackedAt = DateTime.UtcNow, Status = PackListStatus.Open
        });
        order.Status = OrderStatus.Packed;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(orderId);
    }

    public async Task<ShipmentResponseDto> ShipOrderAsync(Guid orderId, CreateShipmentDto dto, Guid userId)
    {
        var order = await _db.OutboundOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException("Order not found.");
        if (order.Status != OrderStatus.Packed)
            throw new BusinessRuleException("Only packed orders can be shipped.");
        if (!await _db.Carriers.AnyAsync(c => c.Id == dto.CarrierId && c.IsActive))
            throw new BusinessRuleException("Carrier not found or inactive.");

        var tasks = await _db.PickTasks
            .Where(t => t.OrderItem.OrderId == orderId && t.Status == PickTaskStatus.Picked && t.QtyPicked > 0)
            .ToListAsync();

        var touched = new List<Guid>();
        foreach (var task in tasks)
        {
            var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == task.ProductId && i.LocationId == task.FromLocationId)
                ?? throw new ConflictException("Stock record for a picked line is missing.");
            if (inv.QtyOnHand < task.QtyPicked)
                throw new ConflictException("Stock on hand is lower than the picked quantity; check recent adjustments.");

            var before = inv.QtyOnHand;
            inv.QtyOnHand -= task.QtyPicked;
            inv.QtyReserved = Math.Max(0, inv.QtyReserved - task.QtyPicked);
            StockHelper.LogTransaction(_db, task.ProductId, task.FromLocationId, ReferenceType.Outbound, orderId,
                TransactionType.Out, before, inv.QtyOnHand, task.QtyPicked, userId, inv.LotNumber);
            touched.Add(task.FromLocationId);
        }

        foreach (var item in order.Items)
        {
            item.ShippedQty = item.PickedQty;
            item.Status = OrderItemStatus.Shipped;
        }

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(), OrderId = orderId, CarrierId = dto.CarrierId, TrackingNo = dto.TrackingNo,
            BolNumber = dto.BolNumber, VehiclePlate = dto.VehiclePlate, DriverName = dto.DriverName,
            Status = ShipmentStatus.Dispatched, ShippedAt = DateTime.UtcNow
        };
        order.Status = OrderStatus.Shipped;
        _db.Shipments.Add(shipment);

        await SaveWithConflictMessage("Stock changed while shipping. Please retry.");
        await StockHelper.SyncLocationStatusAsync(_db, touched.ToArray());

        await _db.Entry(shipment).Reference(s => s.Order).LoadAsync();
        await _db.Entry(shipment).Reference(s => s.Carrier).LoadAsync();
        return shipment.ToDto();
    }

    public async Task<OutboundOrderResponseDto?> CancelAsync(Guid orderId)
    {
        var order = await _db.OutboundOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return null;
        if (order.Status is OrderStatus.Shipped or OrderStatus.Completed or OrderStatus.Cancelled)
            throw new BusinessRuleException($"A {order.Status} order cannot be cancelled.");

        // Release everything still reserved for this order
        var tasks = await _db.PickTasks
            .Where(t => t.OrderItem.OrderId == orderId && t.Status != PickTaskStatus.Cancelled)
            .ToListAsync();
        foreach (var task in tasks)
        {
            var held = task.Status == PickTaskStatus.Picked ? task.QtyPicked : task.QtyToPick;
            var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.ProductId == task.ProductId && i.LocationId == task.FromLocationId);
            if (inv != null) inv.QtyReserved = Math.Max(0, inv.QtyReserved - held);
            task.Status = PickTaskStatus.Cancelled;
        }

        order.Status = OrderStatus.Cancelled;
        await SaveWithConflictMessage("Stock changed while cancelling. Please retry.");
        return await GetByIdAsync(orderId);
    }

    // ---------------------------------------------------------------- shipments

    public async Task<IEnumerable<ShipmentResponseDto>> GetShipmentsAsync()
    {
        var shipments = await _db.Shipments.Include(s => s.Order).Include(s => s.Carrier)
            .OrderByDescending(s => s.ShippedAt).ToListAsync();
        return shipments.Select(s => s.ToDto()).ToList();
    }

    public async Task<ShipmentResponseDto?> MarkDeliveredAsync(Guid shipmentId)
    {
        var shipment = await _db.Shipments.Include(s => s.Order).Include(s => s.Carrier)
            .FirstOrDefaultAsync(s => s.Id == shipmentId);
        if (shipment == null) return null;
        if (shipment.Status == ShipmentStatus.Delivered)
            throw new BusinessRuleException("The shipment is already delivered.");

        shipment.Status = ShipmentStatus.Delivered;
        shipment.DeliveredAt = DateTime.UtcNow;
        shipment.Order.Status = OrderStatus.Completed;
        await _db.SaveChangesAsync();
        return shipment.ToDto();
    }

    private async Task SaveWithConflictMessage(string message)
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(message); }
    }
}
