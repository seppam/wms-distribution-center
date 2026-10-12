using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class OutboundFlowTests
{
    private static async Task<(OutboundService svc, Guid orderId)> ArrangeOrderAsync(TestFixture f, decimal stock, decimal ordered)
    {
        f.Db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), ProductId = f.Product.Id, LocationId = f.Location.Id, QtyOnHand = stock, Status = InventoryStatus.Available });
        await f.Db.SaveChangesAsync();
        var svc = new OutboundService(f.Db, f.Mapper);
        var order = await svc.CreateAsync(new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, "Store #1", null, null, null), f.User.Id);
        await svc.AddItemAsync(order.Id, new AddOrderItemDto(f.Product.Id, ordered, 10m));
        return (svc, order.Id);
    }

    [Fact]
    public async Task StartPicking_ReservesStockAndCreatesPickTasks()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeOrderAsync(f, 50, 20);

        var order = await svc.StartPickingAsync(id);

        Assert.Equal(OrderStatus.Picked, order!.Status);
        var task = await f.Db.PickTasks.SingleAsync();
        Assert.Equal(20, task.QtyToPick);
        Assert.Equal(20, (await f.Db.Inventories.SingleAsync()).QtyReserved);
    }

    [Fact]
    public async Task StartPicking_OnlyAllowedFromDraft()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeOrderAsync(f, 50, 20);
        await svc.StartPickingAsync(id);

        Assert.Null(await svc.StartPickingAsync(id));
    }

    [Fact]
    public async Task FullFlow_Pick_Pack_Ship_DeductsInventory()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeOrderAsync(f, 50, 20);

        await svc.StartPickingAsync(id);
        var packed = await svc.PackOrderAsync(id, new PackOrderDto("CTN-1", 12.5m), f.User.Id);
        Assert.Equal(OrderStatus.Packed, packed!.Status);

        await svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, "TRK1", null, "B 1234 XY", "Driver"), f.User.Id);

        var inv = await f.Db.Inventories.SingleAsync();
        Assert.Equal(30, inv.QtyOnHand);
        Assert.Equal(0, inv.QtyReserved);
        Assert.Equal(OrderStatus.Shipped, (await f.Db.OutboundOrders.SingleAsync()).Status);
        Assert.Contains(await f.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == TransactionType.Out && t.UomQty == 20);
    }

    [Fact]
    public async Task Ship_UnpackedOrder_IsRejected()
    {
        using var f = new TestFixture();
        var (svc, id) = await ArrangeOrderAsync(f, 50, 20);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ShipOrderAsync(id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id));
        Assert.Equal(50, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }
}

public class InventoryAdjustmentTests
{
    [Fact]
    public async Task Adjustment_AppliesOnlyAfterApproval()
    {
        using var f = new TestFixture();
        f.Db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), ProductId = f.Product.Id, LocationId = f.Location.Id, QtyOnHand = 100, Status = InventoryStatus.Available });
        await f.Db.SaveChangesAsync();
        var svc = new InventoryService(f.Db, f.Mapper);

        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Negative, 15, "Damaged"), f.User.Id);
        Assert.Equal(100, (await f.Db.Inventories.SingleAsync()).QtyOnHand);

        await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id);
        Assert.Equal(85, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        Assert.Null(await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id)); // cannot approve twice
    }
}
