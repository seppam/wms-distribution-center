using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WmsDistributionCenter.Tests;

/// <summary>
/// Drives the whole warehouse cycle through the HTTP API with the different roles:
/// receive -> put-away -> order -> pick -> pack -> ship -> adjustment -> transfer -> return.
/// </summary>
public class WorkflowApiTests : IAsyncLifetime
{
    private ApiFactory _f = null!;
    private HttpClient _admin = null!, _sup = null!, _op = null!, _viewer = null!;

    public async Task InitializeAsync()
    {
        _f = await ApiFactory.CreateSeededAsync();
        _admin = await _f.ClientAsAsync("admin");
        _sup = await _f.ClientAsAsync("supervisor");
        _op = await _f.ClientAsAsync("operator");
        _viewer = await _f.ClientAsAsync("viewer");
    }

    public Task DisposeAsync() { _f.Dispose(); return Task.CompletedTask; }

    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"expected {expected} got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Guid Id(JsonElement e, string name = "id") => e.GetProperty(name).GetGuid();

    [Fact]
    public async Task FullWarehouseCycle_ThroughTheApi()
    {
        var warehouses = await Json(await _op.GetAsync("/api/warehouses"));
        var wh = Id(warehouses[0]);
        var suppliers = await Json(await _op.GetAsync("/api/suppliers"));
        var carriers = await Json(await _op.GetAsync("/api/carriers"));
        var products = (await Json(await _op.GetAsync("/api/products"))).EnumerateArray().ToList();
        var rice = products.First(p => p.GetProperty("sku").GetString() == "SKU-1001");
        var locations = (await Json(await _op.GetAsync("/api/locations"))).EnumerateArray().ToList();
        var freeBin = locations.First(l => l.GetProperty("status").GetString() == "Empty");
        var binB = locations.Where(l => l.GetProperty("status").GetString() == "Empty").Skip(1).First();
        var riceId = Id(rice);

        // --- inbound: supervisor plans, operator receives + puts away, completes
        var receipt = await Json(await _sup.PostAsJsonAsync("/api/inboundreceipts", new
        {
            supplierId = Id(suppliers[0]), warehouseId = wh, poNumber = "PO-E2E",
            items = new[] { new { productId = riceId, expectedQty = 40, unitCost = 2.5, lotNumber = "LOT-E2E" } }
        }), HttpStatusCode.Created);
        var receiptId = Id(receipt);
        var lineId = Id(receipt.GetProperty("items")[0]);
        var received = await Json(await _op.PostAsJsonAsync($"/api/inboundreceipts/{receiptId}/receive",
            new { receiptItemId = lineId, receivedQty = 40, damagedQty = 0, putawayLocationId = Id(freeBin) }));
        Assert.Equal("Received", received.GetProperty("status").GetString());
        var done = await Json(await _op.PostAsync($"/api/inboundreceipts/{receiptId}/complete", null));
        Assert.Equal("Received", done.GetProperty("status").GetString());

        // --- outbound: supervisor creates + allocates, operator picks, packs, ships
        var order = await Json(await _sup.PostAsJsonAsync("/api/outboundorders", new
        {
            warehouseId = wh, orderType = "Sales", priority = "Normal", destination = "Store 7",
            items = new[] { new { productId = riceId, orderedQty = 25, unitPrice = 4 } }
        }), HttpStatusCode.Created);
        var orderId = Id(order);
        await Json(await _sup.PostAsync($"/api/outboundorders/{orderId}/start-picking", null));
        var tasks = (await Json(await _op.GetAsync($"/api/pick-tasks?orderId={orderId}"))).EnumerateArray().ToList();
        Assert.NotEmpty(tasks);
        var overPick = await _op.PostAsJsonAsync($"/api/pick-tasks/{Id(tasks[0])}/confirm", new { qtyPicked = 9999 });
        Assert.Equal(HttpStatusCode.BadRequest, overPick.StatusCode);
        foreach (var t in tasks)
            await Json(await _op.PostAsJsonAsync($"/api/pick-tasks/{Id(t)}/confirm", new { qtyPicked = t.GetProperty("qtyToPick").GetDecimal() }));
        var packed = await Json(await _op.PostAsJsonAsync($"/api/outboundorders/{orderId}/pack", new { cartonNo = "CTN-E2E", weightKg = 12.5 }));
        Assert.Equal("Packed", packed.GetProperty("status").GetString());
        var shipment = await Json(await _op.PostAsJsonAsync($"/api/outboundorders/{orderId}/ship",
            new { carrierId = Id(carriers[0]), trackingNo = "TRK-E2E" }));
        Assert.Equal("Dispatched", shipment.GetProperty("status").GetString());
        var detail = await Json(await _viewer.GetAsync($"/api/outboundorders/{orderId}"));
        Assert.Equal("Shipped", detail.GetProperty("status").GetString());
        Assert.Equal(25, detail.GetProperty("items")[0].GetProperty("shippedQty").GetDecimal());

        // --- adjustment: operator proposes, supervisor approves
        var adj = await Json(await _op.PostAsJsonAsync("/api/inventory/adjustments",
            new { productId = riceId, locationId = Id(freeBin), adjType = "Negative", adjQty = 2, reason = "Cycle count" }));
        var denied = await _op.PostAsync($"/api/inventory/adjustments/{Id(adj)}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var approved = await Json(await _sup.PostAsync($"/api/inventory/adjustments/{Id(adj)}/approve", null));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        // --- transfer: operator creates and completes
        var transfer = await Json(await _op.PostAsJsonAsync("/api/transfers",
            new { productId = riceId, fromLocationId = Id(freeBin), toLocationId = Id(binB), qty = 3, reason = "Consolidate" }), HttpStatusCode.Created);
        var completed = await Json(await _op.PostAsync($"/api/transfers/{Id(transfer)}/complete", null));
        Assert.Equal("Completed", completed.GetProperty("status").GetString());

        // --- return: operator receives, supervisor restocks
        var ret = await Json(await _op.PostAsJsonAsync("/api/returns",
            new { warehouseId = wh, productId = riceId, qty = 4, condition = "Good", orderId, reason = "Wrong size" }), HttpStatusCode.Created);
        var denyProcess = await _op.PostAsJsonAsync($"/api/returns/{Id(ret)}/process", new { disposition = "Restock", restockLocationId = Id(binB) });
        Assert.Equal(HttpStatusCode.Forbidden, denyProcess.StatusCode);
        var processed = await Json(await _sup.PostAsJsonAsync($"/api/returns/{Id(ret)}/process", new { disposition = "Restock", restockLocationId = Id(binB) }));
        Assert.Equal("Completed", processed.GetProperty("status").GetString());

        // --- ledger: 40 in, 25 out, -2 adjust, 3 out + 3 in transfer, 4 in return
        var ledger = (await Json(await _viewer.GetAsync($"/api/inventory/transactions?productId={riceId}&take=100"))).EnumerateArray().ToList();
        var kinds = ledger.Select(t => t.GetProperty("referenceType").GetString()).ToHashSet();
        Assert.True(kinds.IsSupersetOf(["Inbound", "Outbound", "Adjustment", "Transfer", "Return"]), string.Join(",", kinds));

        // stock in binB: 3 transferred + 4 returned
        var inv = (await Json(await _viewer.GetAsync($"/api/inventory?locationId={Id(binB)}"))).EnumerateArray().Single();
        Assert.Equal(7, inv.GetProperty("qtyOnHand").GetDecimal());
    }

    [Fact]
    public async Task Viewer_CanReadEverything_ButChangeNothing()
    {
        Assert.Equal(HttpStatusCode.OK, (await _viewer.GetAsync("/api/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _viewer.GetAsync("/api/pick-tasks")).StatusCode);
        var wh = Id((await Json(await _viewer.GetAsync("/api/warehouses")))[0]);
        var res = await _viewer.PostAsJsonAsync("/api/outboundorders", new { warehouseId = wh, orderType = "Sales", priority = "Normal" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task BusinessRuleViolations_ComeBackAs400WithAMessage()
    {
        var wh = Id((await Json(await _sup.GetAsync("/api/warehouses")))[0]);
        var products = await Json(await _sup.GetAsync("/api/products"));
        var order = await Json(await _sup.PostAsJsonAsync("/api/outboundorders", new
        {
            warehouseId = wh, orderType = "Sales", priority = "Normal",
            items = new[] { new { productId = Id(products[0]), orderedQty = 999999, unitPrice = 1 } }
        }), HttpStatusCode.Created);

        var res = await _sup.PostAsync($"/api/outboundorders/{Id(order)}/start-picking", null);
        var body = await Json(res, HttpStatusCode.BadRequest);
        Assert.Contains("Insufficient available stock", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvalidInput_IsRejectedByValidation()
    {
        var products = await Json(await _sup.GetAsync("/api/products"));
        var wh = Id((await Json(await _sup.GetAsync("/api/warehouses")))[0]);
        var res = await _sup.PostAsJsonAsync("/api/outboundorders", new
        {
            warehouseId = wh, orderType = "Sales", priority = "Normal",
            items = new[] { new { productId = Id(products[0]), orderedQty = -5, unitPrice = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
