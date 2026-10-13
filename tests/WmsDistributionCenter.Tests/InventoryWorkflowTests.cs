using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.DTOs.Return;
using WmsDistributionCenter.Api.DTOs.Transfer;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class InventoryAdjustmentTests
{
    [Fact]
    public async Task Adjustment_AppliesOnlyAfterApproval_AndIsLogged()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 100);
        var svc = new InventoryService(f.Db);

        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Negative, 15, "Damaged"), f.User.Id);
        Assert.Equal(100, (await f.Db.Inventories.SingleAsync()).QtyOnHand);

        var approved = await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id);
        Assert.Equal(AdjStatus.Approved, approved!.Status);
        Assert.Equal(85, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        var tx = await f.Db.InventoryTransactions.SingleAsync();
        Assert.Equal((TransactionType.Adjust, 100m, 85m, -15m), (tx.TransactionType, tx.QtyBefore, tx.QtyAfter, tx.UomQty));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ApproveAdjustmentAsync(adj.Id, f.User.Id)); // cannot approve twice
    }

    [Fact]
    public async Task Recount_SetsTheCountedQuantity()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 100);
        var svc = new InventoryService(f.Db);
        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Recount, 93, "Cycle count"), f.User.Id);
        await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id);
        var inv = await f.Db.Inventories.SingleAsync();
        Assert.Equal(93, inv.QtyOnHand);
        Assert.NotNull(inv.LastCountedDate);
    }

    [Fact]
    public async Task PositiveAdjustment_CreatesStockWhereNoneExisted()
    {
        using var f = new TestFixture();
        var svc = new InventoryService(f.Db);
        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Positive, 7, "Found"), f.User.Id);
        await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id);
        Assert.Equal(7, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        Assert.Equal(LocationStatus.Occupied, (await f.Db.Locations.FindAsync(f.Location.Id))!.Status);
    }

    [Fact]
    public async Task NegativeAdjustment_BelowZero_IsRejectedWhenCreated()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 10);
        var svc = new InventoryService(f.Db);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Negative, 11, null), f.User.Id));
    }

    [Fact]
    public async Task Approval_IsRecalculatedAgainstCurrentStock_AndProtectsReservations()
    {
        using var f = new TestFixture();
        var inv = f.Stock(f.Location, 100);
        var svc = new InventoryService(f.Db);
        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Negative, 30, null), f.User.Id);

        inv.QtyReserved = 80; // an order allocated stock after the draft was written
        await f.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ApproveAdjustmentAsync(adj.Id, f.User.Id));
        Assert.Contains("reserved", ex.Message);
        Assert.Equal(100, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task NonAdmins_CannotApproveTheirOwnAdjustment()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 10);
        var svc = new InventoryService(f.Db);
        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Positive, 5, null), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ApproveAdjustmentAsync(adj.Id, f.User.Id, isAdmin: false));
        Assert.NotNull(await svc.ApproveAdjustmentAsync(adj.Id, f.User.Id, isAdmin: true));
    }

    [Fact]
    public async Task RejectedAdjustment_NeverTouchesStock()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 10);
        var svc = new InventoryService(f.Db);
        var adj = await svc.CreateAdjustmentAsync(new CreateAdjustmentDto(f.Product.Id, f.Location.Id, AdjType.Positive, 5, null), f.User.Id);

        var rejected = await svc.RejectAdjustmentAsync(adj.Id, f.User.Id);

        Assert.Equal(AdjStatus.Cancelled, rejected!.Status);
        Assert.Equal(10, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ApproveAdjustmentAsync(adj.Id, f.User.Id));
    }

    [Fact]
    public async Task Query_ClampsPageSize()
    {
        using var f = new TestFixture();
        f.Stock(f.Location, 10);
        var svc = new InventoryService(f.Db);
        var all = await svc.QueryAsync(new InventoryQueryDto(null, null, null, null, Page: 0, PageSize: 100000));
        Assert.Single(all);
    }
}

public class TransferTests
{
    [Fact]
    public async Task CompletedTransfer_MovesStockAndWritesTwoLedgerEntries()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        f.Stock(f.Location, 50, lot: "L1");
        var svc = new TransferService(f.Db);

        var t = await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 20, "Consolidate"), f.User.Id);
        Assert.Equal(TransferStatus.Draft, t.Status);
        Assert.Equal(50, (await f.Db.Inventories.SingleAsync()).QtyOnHand); // nothing moved yet

        var done = await svc.CompleteAsync(t.Id, f.User.Id);

        Assert.Equal(TransferStatus.Completed, done!.Status);
        var rows = await f.Db.Inventories.ToListAsync();
        Assert.Equal(30, rows.Single(r => r.LocationId == f.Location.Id).QtyOnHand);
        var target = rows.Single(r => r.LocationId == dest.Id);
        Assert.Equal((20m, "L1"), (target.QtyOnHand, target.LotNumber));
        var tx = await f.Db.InventoryTransactions.ToListAsync();
        Assert.Contains(tx, x => x.TransactionType == TransactionType.Out && x.LocationId == f.Location.Id && x.UomQty == 20);
        Assert.Contains(tx, x => x.TransactionType == TransactionType.In && x.LocationId == dest.Id && x.UomQty == 20);
        Assert.Equal(LocationStatus.Occupied, (await f.Db.Locations.FindAsync(dest.Id))!.Status);
    }

    [Fact]
    public async Task EmptyingTheSourceBin_MarksItEmpty()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        f.Stock(f.Location, 10);
        var svc = new TransferService(f.Db);
        var t = await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 10, null), f.User.Id);
        await svc.CompleteAsync(t.Id, f.User.Id);
        Assert.Equal(LocationStatus.Empty, (await f.Db.Locations.FindAsync(f.Location.Id))!.Status);
    }

    [Fact]
    public async Task ReservedStock_CannotBeTransferred()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        f.Stock(f.Location, 50, reserved: 40);
        var svc = new TransferService(f.Db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 11, null), f.User.Id));
        Assert.NotNull(await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 10, null), f.User.Id));
    }

    [Fact]
    public async Task Transfer_Rules()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        f.Stock(f.Location, 50);
        var svc = new TransferService(f.Db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, f.Location.Id, 1, null), f.User.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, Guid.NewGuid(), 1, null), f.User.Id));

        var t = await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 5, null), f.User.Id);
        await svc.CompleteAsync(t.Id, f.User.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteAsync(t.Id, f.User.Id)); // not twice
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CancelAsync(t.Id));
    }

    [Fact]
    public async Task CancelledTransfer_MovesNothing()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        f.Stock(f.Location, 50);
        var svc = new TransferService(f.Db);
        var t = await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 5, null), f.User.Id);

        await svc.CancelAsync(t.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteAsync(t.Id, f.User.Id));
        Assert.Equal(50, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task Complete_FailsWhenStockWasReservedInTheMeantime()
    {
        using var f = new TestFixture();
        var dest = f.NewLocation("A-02-01");
        var inv = f.Stock(f.Location, 50);
        var svc = new TransferService(f.Db);
        var t = await svc.CreateAsync(new CreateTransferDto(f.Product.Id, f.Location.Id, dest.Id, 30, null), f.User.Id);

        inv.QtyReserved = 40;
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteAsync(t.Id, f.User.Id));
        Assert.Equal(50, (await f.Db.Inventories.AsNoTracking().SingleAsync()).QtyOnHand);
    }
}

public class ReturnTests
{
    private static async Task<Guid> ShipAsync(TestFixture f, decimal qty)
    {
        f.Stock(f.Location, 100);
        var outbound = new OutboundService(f.Db);
        var order = await outbound.CreateAsync(new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, "Store", null, null, null,
            [new AddOrderItemDto(f.Product.Id, qty, 5)]), f.User.Id);
        await outbound.StartPickingAsync(order.Id);
        foreach (var t in await outbound.GetPickTasksAsync(null, order.Id, null))
            await outbound.ConfirmPickAsync(t.Id, new ConfirmPickDto(t.QtyToPick), f.User.Id);
        await outbound.PackOrderAsync(order.Id, new PackOrderDto("C1", 1), f.User.Id);
        await outbound.ShipOrderAsync(order.Id, new CreateShipmentDto(f.Carrier.Id, null, null, null, null), f.User.Id);
        return order.Id;
    }

    [Fact]
    public async Task GoodReturn_CanBeRestocked_AndIsLogged()
    {
        using var f = new TestFixture();
        var orderId = await ShipAsync(f, 20); // 80 left
        var svc = new ReturnService(f.Db);

        var ret = await svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 5, ReturnCondition.Good, orderId, "Customer changed mind", null), f.User.Id);
        Assert.Equal(ReturnStatus.Received, ret.Status);
        Assert.StartsWith("RET-", ret.ReturnNo);

        var done = await svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Restock, f.Location.Id, null), f.User.Id);

        Assert.Equal(ReturnStatus.Completed, done!.Status);
        Assert.Equal(85, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        Assert.Contains(await f.Db.InventoryTransactions.ToListAsync(), t => t.ReferenceType == ReferenceType.Return && t.UomQty == 5);
    }

    [Fact]
    public async Task DamagedReturn_CannotBeRestocked_ButCanBeDisposed()
    {
        using var f = new TestFixture();
        var orderId = await ShipAsync(f, 20);
        var svc = new ReturnService(f.Db);
        var ret = await svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 3, ReturnCondition.Damaged, orderId, null, null), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Restock, f.Location.Id, null), f.User.Id));
        var done = await svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Dispose, null, "Binned"), f.User.Id);

        Assert.Equal(Disposition.Dispose, done!.Disposition);
        Assert.Equal(80, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Dispose, null, null), f.User.Id));
    }

    [Fact]
    public async Task Return_CannotExceedTheShippedQuantity_AcrossReturns()
    {
        using var f = new TestFixture();
        var orderId = await ShipAsync(f, 10);
        var svc = new ReturnService(f.Db);

        await svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 8, ReturnCondition.Good, orderId, null, null), f.User.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 3, ReturnCondition.Good, orderId, null, null), f.User.Id));
        Assert.NotNull(await svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 2, ReturnCondition.Good, orderId, null, null), f.User.Id));
    }

    [Fact]
    public async Task Return_Rules()
    {
        using var f = new TestFixture();
        var svc = new ReturnService(f.Db);
        f.Stock(f.Location, 10);
        var outbound = new OutboundService(f.Db);
        var draft = await outbound.CreateAsync(new CreateOutboundOrderDto(f.Warehouse.Id, OrderType.Sales, Priority.Normal, null, null, null, null,
            [new AddOrderItemDto(f.Product.Id, 1, 1)]), f.User.Id);

        // order not shipped
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 1, ReturnCondition.Good, draft.Id, null, null), f.User.Id));

        // standalone return, restock needs a location in the same warehouse
        var ret = await svc.CreateAsync(new CreateReturnDto(f.Warehouse.Id, f.Product.Id, 1, ReturnCondition.Good, null, null, null), f.User.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Restock, null, null), f.User.Id));
        var other = new WmsDistributionCenter.Api.Models.Warehouse { Id = Guid.NewGuid(), Code = "DC-2", Name = "Other", IsActive = true };
        f.Db.Warehouses.Add(other);
        var foreign = f.NewLocation("Z-01-01", other);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ProcessAsync(ret.Id, new ProcessReturnDto(Disposition.Restock, foreign.Id, null), f.User.Id));
    }
}
