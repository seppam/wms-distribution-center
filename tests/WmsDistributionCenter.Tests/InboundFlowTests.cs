using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class InboundFlowTests
{
    private static async Task<(TestFixture f, InboundService svc, Guid receiptId, Guid itemId)> ArrangeAsync(decimal expected)
    {
        var f = new TestFixture();
        var svc = new InboundService(f.Db);
        var receipt = await svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, "PO-1", null, null, "D1", null));
        var item = await svc.AddItemAsync(receipt.Id, new AddReceiptItemDto(f.Product.Id, expected, 5m, null, null, null));
        return (f, svc, receipt.Id, item.Id);
    }

    [Fact]
    public async Task CreateReceipt_StartsAsDraft_WithGeneratedNumber()
    {
        using var f = new TestFixture();
        var svc = new InboundService(f.Db);
        var r = await svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, null, null, null, null, null));
        Assert.Equal(InboundStatus.Draft, r.Status);
        Assert.StartsWith("RCV-", r.ReceiptNo);
    }

    [Fact]
    public async Task CreateReceipt_WithLines_ReturnsThemInTheDetail()
    {
        using var f = new TestFixture();
        var svc = new InboundService(f.Db);
        var r = await svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, "PO-9", null, null, null, null,
            [new AddReceiptItemDto(f.Product.Id, 25, 3m, "LOT-1", null, null)]));
        var line = Assert.Single(r.Items!);
        Assert.Equal((25m, "LOT-1"), (line.ExpectedQty, line.LotNumber));
    }

    [Fact]
    public async Task CreateReceipt_RejectsUnknownSupplierOrProduct()
    {
        using var f = new TestFixture();
        var svc = new InboundService(f.Db);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateInboundReceiptDto(Guid.NewGuid(), f.Warehouse.Id, null, null, null, null, null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, null, null, null, null, null,
                [new AddReceiptItemDto(Guid.NewGuid(), 1, 1, null, null, null)])));
        Assert.Empty(f.Db.InboundReceipts);
    }

    [Fact]
    public async Task ReceiveItem_PutsStockAwayAndLogsTransaction()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;

        var res = await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 100, 0, f.Location.Id, null, null), f.User.Id);

        Assert.Equal(ReceiptItemStatus.Received, res!.Status);
        Assert.Equal("A-01-01", res.PutawayLocationCode);
        var inv = await f.Db.Inventories.SingleAsync();
        Assert.Equal(100, inv.QtyOnHand);
        Assert.Equal(LocationStatus.Occupied, (await f.Db.Locations.FindAsync(f.Location.Id))!.Status);
        var tx = await f.Db.InventoryTransactions.SingleAsync();
        Assert.Equal(TransactionType.In, tx.TransactionType);
        Assert.Equal(0, tx.QtyBefore);
        Assert.Equal(100, tx.QtyAfter);
    }

    [Fact]
    public async Task ReceiveItem_Twice_DoesNotDoubleCountStock()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;

        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 60, 0, f.Location.Id, null, null), f.User.Id);
        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 100, 0, f.Location.Id, null, null), f.User.Id);

        Assert.Equal(100, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
        Assert.Equal(2, await f.Db.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task ReceiveItem_DamagedGoods_AreRecordedButNotStocked()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;

        var res = await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 100, 8, f.Location.Id, null, null), f.User.Id);

        Assert.Equal(ReceiptItemStatus.Damaged, res!.Status);
        Assert.Equal(92, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task ReceiveItem_RequiresALocationInTheReceiptsWarehouse()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;
        var otherWh = new Warehouse { Id = Guid.NewGuid(), Code = "DC-2", Name = "Other", IsActive = true };
        f.Db.Warehouses.Add(otherWh);
        var foreign = f.NewLocation("Z-01-01", otherWh);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, null, null, null), f.User.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, foreign.Id, null, null), f.User.Id));
        Assert.Empty(f.Db.Inventories);
    }

    [Fact]
    public async Task ReceiveItem_DamagedMoreThanReceived_IsRejected()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 5, 6, f.Location.Id, null, null), f.User.Id));
    }

    [Fact]
    public async Task ReceiveItem_IntoABinHoldingAnotherLot_IsRejected()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;
        f.Stock(f.Location, 20, lot: "OLD-LOT");

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, f.Location.Id, "NEW-LOT", null), f.User.Id));
        Assert.Equal(20, (await f.Db.Inventories.SingleAsync()).QtyOnHand);
    }

    [Fact]
    public async Task ReceiveItem_ReducingBelowReservedStock_IsRejected()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;
        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 100, 0, f.Location.Id, null, null), f.User.Id);
        (await f.Db.Inventories.SingleAsync()).QtyReserved = 90;
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 50, 0, f.Location.Id, null, null), f.User.Id));
    }

    [Fact]
    public async Task CompleteReceipt_WithShortage_IsPartial()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;

        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 80, 0, f.Location.Id, null, null), f.User.Id);
        var done = await svc.CompleteReceiptAsync(rid, f.User.Id);

        Assert.Equal(InboundStatus.Partial, done!.Status);
    }

    [Fact]
    public async Task CompleteReceipt_FullyReceived_IsReceived()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;

        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, f.Location.Id, null, null), f.User.Id);
        var done = await svc.CompleteReceiptAsync(rid, f.User.Id);

        Assert.Equal(InboundStatus.Received, done!.Status);
    }

    [Fact]
    public async Task CompleteReceipt_Rules()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteReceiptAsync(rid, f.User.Id)); // nothing received yet
        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, f.Location.Id, null, null), f.User.Id);
        await svc.CompleteReceiptAsync(rid, f.User.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteReceiptAsync(rid, f.User.Id)); // twice
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 12, 0, f.Location.Id, null, null), f.User.Id));
    }

    [Fact]
    public async Task OnlyDraftReceipts_CanBeEditedOrDeleted()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(10);
        using var _ = f;
        await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 10, 0, f.Location.Id, null, null), f.User.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(rid, new UpdateInboundReceiptDto("PO-2", null, null, null, null)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.DeleteAsync(rid));
    }
}
