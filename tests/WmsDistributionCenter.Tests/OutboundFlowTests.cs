using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class OutboundFlowTests
{
    private static CreateOutboundOrderDto NewOrder(TestFixture f, params (Product p, decimal qty)[] lines) =>
        new(f.Warehouse.Id, OrderType.Sales, Priority.Normal, "Store #1", null, null, null,
            lines.Select(l => new AddOrderItemDto(l.p.Id, l.qty, 10m)).ToList());

    private static async Task<(OutboundService svc, Guid orderId)> ArrangeAsync(TestFixture f, decimal stock, decimal ordered)
    {
        f.Stock(f.Location, stock);
        var svc = new OutboundService(f.Db);
        var order = await svc.CreateAsync(NewOrder(f, (f.Product, ordered)), f.User.Id);
        return (svc, order.Id);
    }

    private static async Task PickAllAsync(TestFixture f, OutboundService svc, Guid orderId)
    {
        foreach (var t in await svc.GetPickTasksAsync(null, orderId, null))
            await svc.ConfirmPickAsync(t.Id, new ConfirmPickDto(t.QtyToPick), f.User.Id);
    }

    [Fact]
    public async Task CreateOrder_WithLines_ReturnsDetail()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        var order = await svc.GetByIdAsync(id);
        Assert.Equal(OrderStatus.Draft, order!.Status);
        Assert.StartsWith("ORD-", order.OrderNumber);
        Assert.Equal(20, Assert.Single(order.Items!).OrderedQty);
    }

    [Fact]
    public async Task StartPicking_ReservesStockAndCreatesPickTasks()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);

        var order = await svc.StartPickingAsync(id);

        Assert.Equal(OrderStatus.Picking, order!.Status);
        var task = await f.Db.PickTasks.SingleAsync();
        Assert.Equal(20, task.QtyToPick);
        Assert.Equal(20, (await f.Db.Inventories.SingleAsync()).QtyReserved);
    }

    [Fact]
    public async Task StartPicking_OnlyAllowedFromDraft()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.StartPickingAsync(id));
        Assert.Equal(20, (await f.Db.Inventories.SingleAsync()).QtyReserved); // nothing reserved twice
    }

    [Fact]
    public async Task StartPicking_WithInsufficientStock_IsRejectedAndReservesNothing()
    {
        using var f = new TestFixture();
        var other = f.NewProduct("SKU-T2");
        f.Stock(f.Location, 50);
        f.Stock(f.NewLocation("B-01-01"), 5, other);
        var svc = new OutboundService(f.Db);
        var order = await svc.CreateAsync(NewOrder(f, (f.Product, 10), (other, 8)), f.User.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.StartPickingAsync(order.Id));

        Assert.Contains("SKU-T2", ex.Message);
        Assert.All(await f.Db.Inventories.ToListAsync(), i => Assert.Equal(0, i.QtyReserved));
        Assert.Empty(f.Db.PickTasks);
        Assert.Equal(OrderStatus.Draft, (await f.Db.OutboundOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task StartPicking_SecondOrderCannotReserveStockAlreadyAllocated()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 100);
        var svc = new OutboundService(f.Db);
        var first = await svc.CreateAsync(NewOrder(f, (f.Product, 60)), f.User.Id);
        var second = await svc.CreateAsync(NewOrder(f, (f.Product, 60)), f.User.Id);

        await svc.StartPickingAsync(first.Id);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.StartPickingAsync(second.Id));

        Assert.Contains("available 40", ex.Message);
        Assert.Equal(60, (await f.Db.Inventories.SingleAsync()).QtyReserved);
    }

    [Fact]
    public async Task StartPicking_TakesStockThatExpiresFirst()
    {
        using var f = new TestFixture();
        var late = f.Stock(f.Location, 100, expiry: new DateOnly(2030, 1, 1));
        var early = f.Stock(f.NewLocation("A-01-02"), 100, expiry: new DateOnly(2028, 1, 1));
        var svc = new OutboundService(f.Db);
        var order = await svc.CreateAsync(NewOrder(f, (f.Product, 30)), f.User.Id);

        await svc.StartPickingAsync(order.Id);

        var task = await f.Db.PickTasks.SingleAsync();
        Assert.Equal(early.LocationId, task.FromLocationId);
        Assert.Equal(0, (await f.Db.Inventories.FindAsync(late.Id))!.QtyReserved);
    }

    [Fact]
    public async Task StartPicking_SpansLocations_WhenOneBinIsNotEnough()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 30);
        f.Stock(f.NewLocation("A-01-02"), 30);
        var svc = new OutboundService(f.Db);
        var order = await svc.CreateAsync(NewOrder(f, (f.Product, 45)), f.User.Id);

        await svc.StartPickingAsync(order.Id);

        var tasks = await f.Db.PickTasks.ToListAsync();
        Assert.Equal(2, tasks.Count);
        Assert.Equal(45, tasks.Sum(t => t.QtyToPick));
    }

    [Fact]
    public async Task ConfirmPick_OverPick_IsRejected()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        var task = (await svc.GetPickTasksAsync(null, id, null)).Single();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ConfirmPickAsync(task.Id, new ConfirmPickDto(21), f.User.Id));

        Assert.Contains("Over-pick", ex.Message);
        Assert.Equal(PickTaskStatus.Assigned, (await f.Db.PickTasks.SingleAsync()).Status);
    }

    [Fact]
    public async Task ConfirmPick_CannotBeConfirmedTwice()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        var task = (await svc.GetPickTasksAsync(null, id, null)).Single();
        await svc.ConfirmPickAsync(task.Id, new ConfirmPickDto(20), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ConfirmPickAsync(task.Id, new ConfirmPickDto(20), f.User.Id));
    }

    [Fact]
    public async Task ConfirmPick_ShortPick_ReleasesTheMissingReservation()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        var task = (await svc.GetPickTasksAsync(null, id, null)).Single();

        await svc.ConfirmPickAsync(task.Id, new ConfirmPickDto(15), f.User.Id);

        Assert.Equal(15, (await f.Db.Inventories.SingleAsync()).QtyReserved);
        var order = await svc.GetByIdAsync(id);
        Assert.Equal(OrderStatus.Picked, order!.Status);
        var line = order.Items!.Single();
        Assert.Equal((15m, OrderItemStatus.Partial), (line.PickedQty, line.Status));
    }

    [Fact]
    public async Task OrderBecomesPicked_OnlyWhenEveryTaskIsConfirmed()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 30);
        f.Stock(f.NewLocation("A-01-02"), 30);
        var svc = new OutboundService(f.Db);
        var order = await svc.CreateAsync(NewOrder(f, (f.Product, 45)), f.User.Id);
        await svc.StartPickingAsync(order.Id);
        var tasks = (await svc.GetPickTasksAsync(null, order.Id, null)).ToList();

        await svc.ConfirmPickAsync(tasks[0].Id, new ConfirmPickDto(tasks[0].QtyToPick), f.User.Id);
        Assert.Equal(OrderStatus.Picking, (await svc.GetByIdAsync(order.Id))!.Status);

        await svc.ConfirmPickAsync(tasks[1].Id, new ConfirmPickDto(tasks[1].QtyToPick), f.User.Id);
        Assert.Equal(OrderStatus.Picked, (await svc.GetByIdAsync(order.Id))!.Status);
    }

    [Fact]
    public async Task Pack_RequiresPickedOrder()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id); // still Picking, nothing confirmed

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 5), f.User.Id));
    }

    [Fact]
    public async Task FullFlow_Pick_Pack_Ship_DeductsInventory()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);

        await svc.StartPickingAsync(id);
        await PickAllAsync(f, svc, id);
        var packed = await svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 12.5m), f.User.Id);
        Assert.Equal(OrderStatus.Packed, packed!.Status);
        Assert.Equal("CTN-1", Assert.Single(packed.PackLists!).CartonNo);

        var shipment = await svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, "TRK1", null, "B 1234 XY", "Driver"), f.User.Id);

        var inv = await f.Db.Inventories.SingleAsync();
        Assert.Equal(30, inv.QtyOnHand);
        Assert.Equal(0, inv.QtyReserved);
        Assert.Equal(OrderStatus.Shipped, (await f.Db.OutboundOrders.SingleAsync()).Status);
        Assert.Equal(20, (await f.Db.OrderItems.SingleAsync()).ShippedQty);
        Assert.Contains(await f.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == TransactionType.Out && t.UomQty == 20 && t.QtyAfter == 30);

        var delivered = await svc.MarkDeliveredAsync(shipment.Id);
        Assert.Equal(ShipmentStatus.Delivered, delivered!.Status);
        Assert.Equal(OrderStatus.Completed, (await f.Db.OutboundOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task Ship_ShortPickedOrder_DeductsOnlyWhatWasPicked()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        var task = (await svc.GetPickTasksAsync(null, id, null)).Single();
        await svc.ConfirmPickAsync(task.Id, new ConfirmPickDto(12), f.User.Id);
        await svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 5), f.User.Id);

        await svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id);

        var inv = await f.Db.Inventories.SingleAsync();
        Assert.Equal((38m, 0m), (inv.QtyOnHand, inv.QtyReserved));
    }

    [Fact]
    public async Task Ship_UnpackedOrder_IsRejected()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id));
        Assert.Equal(50, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task Ship_Twice_IsRejected()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        await PickAllAsync(f, svc, id);
        await svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 5), f.User.Id);
        await svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id));
        Assert.Equal(30, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task Cancel_ReleasesReservedStock_AndBlocksFurtherSteps()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);

        var cancelled = await svc.CancelAsync(id);

        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);
        Assert.Equal(0, (await f.Db.Inventories.SingleAsync()).QtyReserved);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.StartPickingAsync(id));
    }

    [Fact]
    public async Task Cancel_ShippedOrder_IsRejected()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);
        await PickAllAsync(f, svc, id);
        await svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 5), f.User.Id);
        await svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CancelAsync(id));
    }

    [Fact]
    public async Task EditingAndDeleting_AreOnlyAllowedForDraftOrders()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(id, new UpdateOutboundOrderDto(Priority.Rush, null, null, null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.DeleteAsync(id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AddItemAsync(id, new AddOrderItemDto(f.Product.Id, 1, 1)));
    }

    [Fact]
    public async Task Lines_MustReferenceActiveProducts_AndBeUnique()
    {
        using var f = new TestFixture();
        var svc = new OutboundService(f.Db);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, null, null, null, null,
                [new AddOrderItemDto(Guid.NewGuid(), 1, 1)]), f.User.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, null, null, null, null,
                [new AddOrderItemDto(f.Product.Id, 1, 1), new AddOrderItemDto(f.Product.Id, 2, 1)]), f.User.Id));
        Assert.Empty(f.Db.OutboundOrders); // failed creates leave nothing behind
    }

    [Fact]
    public async Task AssignPicker_RequiresAFloorRole()
    {
        using var f = new TestFixture();
        var viewerRole = new Role { Id = Guid.NewGuid(), Name = "Viewer" };
        var viewer = new User { Id = Guid.NewGuid(), Username = "v", Email = "v@example.com", PasswordHash = "x", FullName = "V", RoleId = viewerRole.Id, IsActive = true };
        f.Db.AddRange(viewerRole, viewer);
        await f.Db.SaveChangesAsync();
        var (svc, id) = await ArrangeAsync(f, 50, 20);
        await svc.StartPickingAsync(id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.AssignPickerAsync(id, new AssignPickerDto(viewer.Id)));
        await svc.AssignPickerAsync(id, new AssignPickerDto(f.User.Id));
        Assert.Equal(f.User.Id, (await f.Db.PickTasks.SingleAsync()).PickerId);
    }
}

public class StockConcurrencyTests
{
    [Fact]
    public async Task StaleInventoryWrite_IsRejectedByTheConcurrencyToken()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 100);
        using var a = f.NewContext();
        using var b = f.NewContext();
        var invA = await a.Inventories.SingleAsync();
        var invB = await b.Inventories.SingleAsync();

        invA.QtyReserved += 60;
        await a.SaveChangesAsync();
        invB.QtyReserved += 60;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        Assert.Equal(1, (await f.Db.Inventories.AsNoTracking().SingleAsync()).Version);
    }

    [Fact]
    public async Task ParallelAllocations_NeverReserveMoreThanIsAvailable()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 100);
        var orderIds = new List<Guid>();
        var seed = new OutboundService(f.Db);
        for (var i = 0; i < 6; i++)
            orderIds.Add((await seed.CreateAsync(
                new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, null, null, null, null,
                    [new AddOrderItemDto(f.Product.Id, 40, 1)]), f.User.Id)).Id);

        var results = await Task.WhenAll(orderIds.Select(async id =>
        {
            using var ctx = f.NewContext();
            try { await new OutboundService(ctx).StartPickingAsync(id); return true; }
            catch (Exception e) when (e is BusinessRuleException or ConflictException) { return false; }
        }));

        var inv = await f.Db.Inventories.AsNoTracking().SingleAsync();
        Assert.True(results.Count(r => r) is >= 1 and <= 2, "at most two 40-unit orders fit into 100 units");
        Assert.True(inv.QtyReserved <= inv.QtyOnHand);
        Assert.Equal(results.Count(r => r) * 40m, inv.QtyReserved);
        Assert.Equal(inv.QtyReserved, await f.Db.PickTasks.AsNoTracking().SumAsync(t => t.QtyToPick));
    }
}
