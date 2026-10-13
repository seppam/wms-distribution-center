using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Tests;

/// <summary>
/// The browser-only demo starts from the SAME dataset as the Docker demo. That dataset is produced by driving the real
/// services (DemoDataSeeder); this test exports the result to contracts/demo-seed.json, and fails when the committed
/// file no longer matches what the backend produces. The frontend adapter loads that file, so the seed has one source.
///
/// Normalisation (so the file is stable): ids become readable stable ids, timestamps become "@seed" (the adapter
/// substitutes the moment the demo data is created), dates become "@d:+N" day offsets from the seeding day, and the
/// seeding day inside document numbers becomes "{today}".
/// </summary>
public class DemoSeedContractTests
{
    // Rows are exported in a deterministic order that does not depend on the database's enumeration order:
    // master data by natural key, documents by creation time, lines by their parent's position and the product SKU.
    private static readonly (string Name, Type Type, Func<WmsDbContext, IEnumerable<object>> Rows)[] Tables =
    [
        ("roles", typeof(Role), d => d.Roles.AsNoTracking().AsEnumerable().OrderBy(r => Array.IndexOf(new[] { "Admin", "Supervisor", "Operator", "Viewer" }, r.Name)).ToList()),
        ("users", typeof(User), d => d.Users.AsNoTracking().AsEnumerable().OrderBy(u => Array.IndexOf(new[] { "admin", "supervisor", "operator", "viewer" }, u.Username)).ToList()),
        ("warehouses", typeof(Warehouse), d => d.Warehouses.AsNoTracking().AsEnumerable().OrderByDescending(w => w.Code, StringComparer.Ordinal).ToList()),
        ("zones", typeof(Zone), d =>
        {
            var wh = d.Warehouses.AsNoTracking().ToDictionary(w => w.Id, w => w.Code);
            return d.Zones.AsNoTracking().AsEnumerable().OrderByDescending(z => wh[z.WarehouseId], StringComparer.Ordinal).ThenBy(z => z.ZoneType).ThenBy(z => z.Name, StringComparer.Ordinal).ToList();
        }),
        ("locations", typeof(Location), d => d.Locations.AsNoTracking().AsEnumerable().OrderBy(l => l.LocationCode, StringComparer.Ordinal).ToList()),
        ("uoms", typeof(Uom), d => d.Uoms.AsNoTracking().AsEnumerable().OrderBy(u => u.ConversionFactor).ToList()),
        ("categories", typeof(Category), d => d.Categories.AsNoTracking().AsEnumerable().OrderBy(c => c.Level).ThenBy(c => c.Name, StringComparer.Ordinal).ToList()),
        ("products", typeof(Product), d => d.Products.AsNoTracking().AsEnumerable().OrderBy(p => p.Sku, StringComparer.Ordinal).ToList()),
        ("suppliers", typeof(Supplier), d => d.Suppliers.AsNoTracking().AsEnumerable().OrderBy(s => s.Code, StringComparer.Ordinal).ToList()),
        ("carriers", typeof(Carrier), d => d.Carriers.AsNoTracking().AsEnumerable().OrderBy(c => c.Name, StringComparer.Ordinal).ToList()),
        ("inventories", typeof(Inventory), d =>
        {
            var sku = d.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Sku);
            var loc = d.Locations.AsNoTracking().ToDictionary(l => l.Id, l => l.LocationCode);
            return d.Inventories.AsNoTracking().AsEnumerable().OrderBy(i => sku[i.ProductId], StringComparer.Ordinal).ThenBy(i => loc[i.LocationId], StringComparer.Ordinal).ToList();
        }),
        ("inventoryTransactions", typeof(InventoryTransaction), d => d.InventoryTransactions.AsNoTracking().AsEnumerable().OrderBy(t => t.Id).ToList()),
        ("inboundReceipts", typeof(InboundReceipt), d => d.InboundReceipts.AsNoTracking().AsEnumerable().OrderBy(r => r.CreatedAt).ThenBy(r => r.PoNumber, StringComparer.Ordinal).ToList()),
        ("receiptItems", typeof(ReceiptItem), d =>
        {
            var rcv = d.InboundReceipts.AsNoTracking().AsEnumerable().OrderBy(r => r.CreatedAt).ThenBy(r => r.PoNumber, StringComparer.Ordinal).Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var sku = d.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Sku);
            return d.ReceiptItems.AsNoTracking().AsEnumerable().OrderBy(i => rcv[i.ReceiptId]).ThenBy(i => sku[i.ProductId], StringComparer.Ordinal).ToList();
        }),
        ("outboundOrders", typeof(OutboundOrder), d => d.OutboundOrders.AsNoTracking().AsEnumerable().OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNumber, StringComparer.Ordinal).ToList()),
        ("orderItems", typeof(OrderItem), d =>
        {
            var ord = d.OutboundOrders.AsNoTracking().AsEnumerable().OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNumber, StringComparer.Ordinal).Select((o, i) => (o.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var sku = d.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Sku);
            return d.OrderItems.AsNoTracking().AsEnumerable().OrderBy(i => ord[i.OrderId]).ThenBy(i => sku[i.ProductId], StringComparer.Ordinal).ToList();
        }),
        ("pickTasks", typeof(PickTask), d =>
        {
            var ord = d.OutboundOrders.AsNoTracking().AsEnumerable().OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNumber, StringComparer.Ordinal).Select((o, i) => (o.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var itemOrder = d.OrderItems.AsNoTracking().ToDictionary(i => i.Id, i => ord[i.OrderId]);
            var sku = d.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Sku);
            var loc = d.Locations.AsNoTracking().ToDictionary(l => l.Id, l => l.LocationCode);
            return d.PickTasks.AsNoTracking().AsEnumerable().OrderBy(t => itemOrder[t.OrderItemId]).ThenBy(t => sku[t.ProductId], StringComparer.Ordinal).ThenBy(t => loc[t.FromLocationId], StringComparer.Ordinal).ToList();
        }),
        ("packLists", typeof(PackList), d =>
        {
            var ord = d.OutboundOrders.AsNoTracking().AsEnumerable().OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNumber, StringComparer.Ordinal).Select((o, i) => (o.Id, i)).ToDictionary(x => x.Id, x => x.i);
            return d.PackLists.AsNoTracking().AsEnumerable().OrderBy(p => ord[p.OrderId]).ThenBy(p => p.CartonNo, StringComparer.Ordinal).ToList();
        }),
        ("shipments", typeof(Shipment), d =>
        {
            var ord = d.OutboundOrders.AsNoTracking().AsEnumerable().OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderNumber, StringComparer.Ordinal).Select((o, i) => (o.Id, i)).ToDictionary(x => x.Id, x => x.i);
            return d.Shipments.AsNoTracking().AsEnumerable().OrderBy(s => ord[s.OrderId]).ToList();
        }),
        ("adjustments", typeof(Adjustment), d => d.Adjustments.AsNoTracking().AsEnumerable().OrderBy(a => a.AdjNumber, StringComparer.Ordinal).ToList()),
        ("transfers", typeof(Transfer), d => d.Transfers.AsNoTracking().AsEnumerable().OrderBy(t => t.TransferNo, StringComparer.Ordinal).ToList()),
        ("returnOrders", typeof(ReturnOrder), d => d.ReturnOrders.AsNoTracking().AsEnumerable().OrderBy(r => r.ReturnNo, StringComparer.Ordinal).ToList()),
    ];

    // Columns that never leave the server: credentials, JSON blobs the app does not use, concurrency tokens.
    private static readonly HashSet<string> Skipped = ["PasswordHash", "Permissions", "Version"];

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];

    public static string Export(WmsDbContext db)
    {
        var today = DateOnly.FromDateTime(db.Warehouses.AsNoTracking().Min(w => w.CreatedAt));
        var token = today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var data = Tables.Select(t => (t.Name, t.Type, Rows: t.Rows(db).ToList())).ToList();

        // First pass: stable ids, "<prefix>-<n>" in creation order (accounts and roles are named after themselves).
        var ids = new Dictionary<Guid, string>();
        foreach (var (name, type, rows) in data)
        {
            var idProp = type.GetProperty("Id")!;
            var n = 0;
            foreach (var row in rows)
            {
                if (idProp.GetValue(row) is not Guid id) continue;
                ids[id] = row switch
                {
                    User u => "u-" + u.Username,
                    Role r => "r-" + r.Name.ToLowerInvariant(),
                    _ => $"{Prefix(name)}-{++n:D3}",
                };
            }
        }

        object? Convert(object? v) => v switch
        {
            null => null,
            Guid g => ids.TryGetValue(g, out var s) ? s : throw new InvalidOperationException($"Unmapped id {g}"),
            DateTime => "@seed",
            DateOnly d => $"@d:{d.DayNumber - today.DayNumber:+0;-0;0}",
            Enum e => e.ToString(),
            string s => s.Replace(token, "{today}"),
            _ => v,
        };

        var sb = new StringBuilder("{\n");
        sb.Append("  \"description\": \"Fictional demo dataset, generated from the real DemoDataSeeder by DemoSeedContractTests (UPDATE_CONTRACTS=1). Ids are stable, \\\"@seed\\\" is the creation moment, \\\"@d:+N\\\" is N days from today, {today} is yyyyMMdd.\",\n");
        sb.Append("  \"tables\": {\n");
        for (var t = 0; t < data.Count; t++)
        {
            var (name, type, rows) = data[t];
            var props = type.GetProperties()
                .Where(p => !Skipped.Contains(p.Name) && IsScalar(p.PropertyType))
                .ToList();
            sb.Append($"    \"{name}\": [\n");
            for (var r = 0; r < rows.Count; r++)
            {
                var obj = new JsonObject();
                foreach (var p in props)
                {
                    var value = Convert(p.GetValue(rows[r]));
                    obj[Camel(p.Name)] = value switch
                    {
                        null => null,
                        string s => JsonValue.Create(s),
                        bool b => JsonValue.Create(b),
                        int i => JsonValue.Create(i),
                        long l => JsonValue.Create(l),
                        decimal m => JsonValue.Create(m),
                        _ => throw new InvalidOperationException($"{type.Name}.{p.Name}: unsupported {value.GetType()}"),
                    };
                }
                sb.Append("      ").Append(obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
                sb.Append(r < rows.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(t < data.Count - 1 ? "    ],\n" : "    ]\n");
        }
        sb.Append("  }\n}\n");
        return sb.ToString();
    }

    private static string Prefix(string table) => table switch
    {
        "inventories" => "inv", "inventoryTransactions" => "txn", "inboundReceipts" => "rcv", "receiptItems" => "rcvl",
        "outboundOrders" => "ord", "orderItems" => "ordl", "pickTasks" => "pick", "packLists" => "pack", "shipments" => "shp",
        "adjustments" => "adj", "transfers" => "trf", "returnOrders" => "ret", "warehouses" => "wh", "zones" => "zone",
        "locations" => "loc", "uoms" => "uom", "categories" => "cat", "products" => "prod", "suppliers" => "sup",
        "carriers" => "car", _ => table,
    };

    private static bool IsScalar(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsEnum || t == typeof(string) || t == typeof(Guid) || t == typeof(bool) || t == typeof(int) || t == typeof(long)
            || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateOnly);
    }

    private static string ExportFromRealSeeder()
    {
        using var f = ApiFactory.CreateDemo();
        using var scope = f.Services.CreateScope();
        return Export(scope.ServiceProvider.GetRequiredService<WmsDbContext>());
    }

    [Fact]
    public void BrowserDemoSeed_MatchesWhatTheRealDemoSeederProduces()
        => Contracts.AssertMatches("demo-seed.json", ExportFromRealSeeder());

    [Fact]
    public void BrowserDemoSeed_IsDeterministic()
        => Assert.Equal(ExportFromRealSeeder(), ExportFromRealSeeder());

    [Fact]
    public void BrowserDemoSeed_HasNoCredentials_AndEveryReferenceResolves()
    {
        using var doc = Contracts.Read("demo-seed.json");
        var tables = doc.RootElement.GetProperty("tables");
        var raw = tables.GetRawText();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ApiFactory.DemoPassword, raw);

        var allIds = tables.EnumerateObject()
            .SelectMany(t => t.Value.EnumerateArray())
            .Where(r => r.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(r => r.GetProperty("id").GetString()!)
            .ToHashSet();
        foreach (var table in tables.EnumerateObject())
            foreach (var row in table.Value.EnumerateArray())
                foreach (var prop in row.EnumerateObject())
                    if (prop.Name.EndsWith("Id") && prop.Name != "id" && prop.Name != "referenceId" && prop.Value.ValueKind == JsonValueKind.String)
                        Assert.True(allIds.Contains(prop.Value.GetString()!), $"{table.Name}.{prop.Name} -> {prop.Value.GetString()} does not exist");
    }
}
