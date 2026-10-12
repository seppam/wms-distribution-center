using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class InboundFlowTests
{
    private static async Task<(TestFixture f, InboundService svc, Guid receiptId, Guid itemId)> ArrangeAsync(decimal expected)
    {
        var f = new TestFixture();
        var svc = new InboundService(f.Db, f.Mapper);
        var receipt = await svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, "PO-1", null, null, "D1", null));
        var item = await svc.AddItemAsync(receipt.Id, new AddReceiptItemDto(f.Product.Id, expected, 5m, null, null, f.Location.Id));
        return (f, svc, receipt.Id, item.Id);
    }

    [Fact]
    public async Task CreateReceipt_StartsAsDraft_WithGeneratedNumber()
    {
        using var f = new TestFixture();
        var svc = new InboundService(f.Db, f.Mapper);
        var r = await svc.CreateAsync(new CreateInboundReceiptDto(f.Supplier.Id, f.Warehouse.Id, null, null, null, null, null));
        Assert.Equal(InboundStatus.Draft, r.Status);
        Assert.StartsWith("RCV-", r.ReceiptNo);
    }

    [Fact]
    public async Task ReceiveItem_PutsStockAwayAndLogsTransaction()
    {
        var (f, svc, rid, iid) = await ArrangeAsync(100);
        using var _ = f;

        var res = await svc.ReceiveItemAsync(rid, new ReceiveItemDto(iid, 100, 0, f.Location.Id, null, null), f.User.Id);

        Assert.Equal(ReceiptItemStatus.Received, res!.Status);
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
}
