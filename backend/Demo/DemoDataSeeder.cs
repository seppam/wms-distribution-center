using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.DTOs.Return;
using WmsDistributionCenter.Api.DTOs.Transfer;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;
using Inbound = WmsDistributionCenter.Api.DTOs.InboundReceipt;

namespace WmsDistributionCenter.Api.Demo;

/// <summary>
/// Builds the fictional demo dataset by driving the real services (receive, allocate, pick, pack, ship, adjust,
/// transfer, return), so every record, stock level and ledger entry is consistent with the business rules.
/// Only ever called when Demo:Enabled is true.
/// </summary>
public sealed class DemoDataSeeder(
    WmsDbContext db, DemoOptions options,
    IInboundService inbound, IOutboundService outbound, IInventoryService inventory,
    ITransferService transfers, IReturnService returns, ILogger<DemoDataSeeder> logger)
{
    /// <summary>One public account per role. The username is the lower-case role name.</summary>
    public static readonly (string Username, string Role, string FullName)[] Accounts =
    [
        ("admin", AppRoles.Admin, "Demo Administrator"),
        ("supervisor", AppRoles.Supervisor, "Demo Supervisor"),
        ("operator", AppRoles.Operator, "Demo Operator"),
        ("viewer", AppRoles.Viewer, "Demo Viewer"),
    ];

    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Startup: roles and demo accounts are always (re)asserted; the dataset is only created on an empty database.</summary>
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await DbSeeder.EnsureRolesAsync(db);
            await EnsureAccountsAsync();
            if (!await db.Warehouses.AnyAsync(ct))
                await SeedDatasetAsync();
        }
        finally { Gate.Release(); }
    }

    /// <summary>Wipes all business data and recreates the dataset. Accounts keep their ids so signed-in sessions stay valid.</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            logger.LogWarning("Resetting the demo dataset");
            await WipeBusinessDataAsync(ct);
            db.ChangeTracker.Clear();
            await DbSeeder.EnsureRolesAsync(db);
            await EnsureAccountsAsync(removeOthers: true);
            await SeedDatasetAsync();
        }
        finally { Gate.Release(); }
    }

    // ------------------------------------------------------------------ accounts

    private async Task EnsureAccountsAsync(bool removeOthers = false)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name);
        var users = await db.Users.ToListAsync();
        foreach (var (username, role, fullName) in Accounts)
        {
            var user = users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                db.Users.Add(DbSeeder.NewUser(username, $"{username}@example.com", fullName, options.Password!, roles[role].Id));
                continue;
            }
            user.RoleId = roles[role].Id;
            user.IsActive = true;
            user.FullName = fullName;
            if (!BCrypt.Net.BCrypt.Verify(options.Password!, user.PasswordHash))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.Password!);
        }
        if (removeOthers)
        {
            var keep = Accounts.Select(a => a.Username).ToHashSet();
            db.Users.RemoveRange(users.Where(u => !keep.Contains(u.Username)));
        }
        await db.SaveChangesAsync();
        if (removeOthers)
        {
            db.Roles.RemoveRange(await db.Roles.Where(r => !r.IsSystem).ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    // ------------------------------------------------------------------ wipe

    private async Task WipeBusinessDataAsync(CancellationToken ct)
    {
        var keep = new[] { typeof(User), typeof(Role) };
        var types = db.Model.GetEntityTypes().Where(t => !t.IsOwned() && !keep.Contains(t.ClrType)).ToList();

        // Children first: a table can be emptied once no remaining table references it.
        var ordered = new List<IEntityType>();
        var remaining = types.ToList();
        while (remaining.Count > 0)
        {
            var next = remaining.Where(t => !remaining.Any(o => o != t && o.GetForeignKeys().Any(fk => fk.PrincipalEntityType == t))).ToList();
            if (next.Count == 0) next = [remaining[0]]; // self-referencing / cyclic: a single DELETE per table is still valid
            ordered.AddRange(next);
            remaining.RemoveAll(next.Contains);
        }

        if (db.Database.IsRelational())
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE users SET warehouse_id = NULL", ct);
            foreach (var t in ordered)
#pragma warning disable EF1002 // table names come from the EF model, never from user input
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{t.GetTableName()}\"", ct);
#pragma warning restore EF1002
            await tx.CommitAsync(ct);
        }
        else
        {
            foreach (var u in await db.Users.ToListAsync(ct)) u.WarehouseId = null;
            var setMethod = typeof(DbContext).GetMethods().First(m => m.Name == nameof(DbContext.Set) && m.GetParameters().Length == 0);
            foreach (var t in ordered)
            {
                var rows = ((System.Collections.IEnumerable)setMethod.MakeGenericMethod(t.ClrType).Invoke(db, null)!).Cast<object>().ToList();
                db.RemoveRange(rows);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    // ------------------------------------------------------------------ dataset

    private sealed record Sku(string Code, string Name, string Cat, string Uom, decimal Kg, decimal Cost, decimal Price, int Min, int Max,
        string Bin, int Qty, int? ShelfDays, string Supplier);

    private static readonly Sku[] Skus =
    [
        new("SKU-1001", "Long Grain Rice 5 kg", "Dry Goods", "PCS", 5, 6.20m, 8.90m, 80, 600, "A-01-01", 420, 365, "SUP-GRN"),
        new("SKU-1002", "Cooking Oil 2 L", "Dry Goods", "PCS", 2, 3.10m, 4.60m, 60, 400, "A-01-02", 260, 300, "SUP-GRN"),
        new("SKU-1003", "Instant Noodles, carton of 30", "Dry Goods", "CTN", 3.2m, 9.00m, 12.50m, 40, 300, "A-01-03", 150, 240, "SUP-GRN"),
        new("SKU-1004", "Granulated Sugar 1 kg", "Dry Goods", "PCS", 1, 1.05m, 1.70m, 120, 800, "A-02-01", 90, 540, "SUP-GRN"),
        new("SKU-1005", "Wheat Flour 1 kg", "Dry Goods", "PCS", 1, 0.85m, 1.40m, 100, 700, "A-02-02", 340, 270, "SUP-GRN"),
        new("SKU-1006", "Canned Tomatoes 400 g, box of 12", "Dry Goods", "BOX", 5.2m, 7.40m, 10.20m, 30, 200, "A-02-03", 75, 720, "SUP-HVF"),
        new("SKU-2001", "UHT Milk 1 L", "Dairy & Chilled", "PCS", 1, 0.95m, 1.45m, 150, 900, "C-01-01", 310, 120, "SUP-NFD"),
        new("SKU-2002", "Plain Yogurt 500 g", "Dairy & Chilled", "PCS", 0.5m, 0.80m, 1.35m, 90, 500, "C-01-02", 120, 21, "SUP-NFD"),
        new("SKU-2003", "Salted Butter 250 g", "Dairy & Chilled", "PCS", 0.25m, 1.90m, 2.80m, 60, 300, "C-02-01", 140, 90, "SUP-NFD"),
        new("SKU-2004", "Cheddar Cheese Block 400 g", "Dairy & Chilled", "PCS", 0.4m, 3.30m, 4.90m, 50, 250, "C-02-02", 28, 60, "SUP-NFD"),
        new("SKU-3001", "Still Water 600 ml, pack of 24", "Beverages", "CTN", 14.4m, 3.60m, 5.50m, 50, 400, "A-03-01", 280, 540, "SUP-BLW"),
        new("SKU-3002", "Orange Juice 1 L", "Beverages", "PCS", 1, 1.40m, 2.20m, 80, 500, "A-03-02", 210, 150, "SUP-BLW"),
        new("SKU-3003", "Black Tea, 100 bags", "Beverages", "BOX", 0.25m, 1.60m, 2.60m, 60, 400, "A-03-03", 165, 600, "SUP-BLW"),
        new("SKU-3004", "Ground Coffee 250 g", "Beverages", "PCS", 0.25m, 3.20m, 4.80m, 40, 300, "B-03-01", 18, 400, "SUP-BLW"),
        new("SKU-4001", "Salted Crackers 200 g", "Snacks", "PCS", 0.2m, 0.70m, 1.25m, 100, 600, "B-01-01", 380, 270, "SUP-SNK"),
        new("SKU-4002", "Potato Chips 150 g, box of 20", "Snacks", "BOX", 3.4m, 8.00m, 11.50m, 30, 200, "B-01-02", 85, 150, "SUP-SNK"),
        new("SKU-4003", "Chocolate Biscuits 300 g", "Snacks", "PCS", 0.3m, 1.10m, 1.85m, 80, 500, "B-01-03", 240, 200, "SUP-SNK"),
        new("SKU-4004", "Roasted Peanuts 500 g", "Snacks", "PCS", 0.5m, 1.80m, 2.90m, 50, 300, "B-02-01", 150, 180, "SUP-SNK"),
        new("SKU-5001", "Dish Soap 750 ml", "Household", "PCS", 0.8m, 1.20m, 2.10m, 70, 400, "B-02-02", 190, null, "SUP-CLH"),
        new("SKU-5002", "Laundry Detergent 3 kg", "Household", "PCS", 3, 5.40m, 8.20m, 40, 250, "B-02-03", 110, null, "SUP-CLH"),
        new("SKU-5003", "Paper Towels, pack of 6", "Household", "PCS", 0.9m, 2.30m, 3.60m, 60, 350, "B-04-01", 205, null, "SUP-CLH"),
        new("SKU-5004", "Trash Bags 30 L, roll of 20", "Household", "PCS", 0.3m, 0.90m, 1.60m, 80, 450, "B-04-02", 64, null, "SUP-CLH"),
        new("SKU-6001", "Hand Soap 250 ml", "Personal Care", "PCS", 0.3m, 0.95m, 1.75m, 60, 350, "B-03-02", 170, null, "SUP-CLH"),
        new("SKU-6002", "Toothpaste 120 g", "Personal Care", "PCS", 0.15m, 0.85m, 1.60m, 80, 450, "B-03-03", 260, null, "SUP-CLH"),
    ];

    private async Task SeedDatasetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var users = await db.Users.Include(u => u.Role).ToDictionaryAsync(u => u.Username);
        var admin = users["admin"].Id;
        var supervisor = users["supervisor"].Id;
        var op = users["operator"].Id;

        // ---- master data (entities written directly: there is no workflow behind them)
        var main = new Warehouse { Id = Guid.NewGuid(), Code = "DC-MAIN", Name = "Central Distribution Center", City = "Sampleton", Address = "1 Example Road", Capacity = 50000, IsActive = true };
        var east = new Warehouse { Id = Guid.NewGuid(), Code = "DC-EAST", Name = "East Cross-Dock", City = "Exampleville", Address = "88 Demo Avenue", Capacity = 12000, IsActive = true };
        db.Warehouses.AddRange(main, east);

        Zone Z(Warehouse w, string name, ZoneType t) => new() { Id = Guid.NewGuid(), WarehouseId = w.Id, Name = name, ZoneType = t, IsActive = true };
        var zReceiving = Z(main, "Receiving Dock", ZoneType.Receiving);
        var zAmbient = Z(main, "Ambient Storage", ZoneType.Storage);
        var zChilled = Z(main, "Chilled Storage", ZoneType.Storage);
        var zPacking = Z(main, "Packing Area", ZoneType.Packing);
        var zShipping = Z(main, "Shipping Dock", ZoneType.Shipping);
        var zReturns = Z(main, "Returns Area", ZoneType.Returns);
        var eReceiving = Z(east, "East Receiving", ZoneType.Receiving);
        var eStorage = Z(east, "East Storage", ZoneType.Storage);
        db.Zones.AddRange(zReceiving, zAmbient, zChilled, zPacking, zShipping, zReturns, eReceiving, eStorage);

        var locs = new Dictionary<string, Location>();
        void Loc(Zone z, string code, LocationType type, decimal cap, string? aisle = null, string? rack = null, string? level = null, string? bin = null) =>
            locs[code] = new Location { Id = Guid.NewGuid(), ZoneId = z.Id, LocationCode = code, Aisle = aisle, Rack = rack, Level = level, Bin = bin, LocationType = type, MaxCapacity = cap, Status = LocationStatus.Empty };
        foreach (var (zone, aisle, racks) in new[] { (zAmbient, "A", 4), (zAmbient, "B", 4), (zChilled, "C", 2) })
            for (var r = 1; r <= racks; r++)
                for (var l = 1; l <= 3; l++)
                    Loc(zone, $"{aisle}-{r:D2}-{l:D2}", l == 1 ? LocationType.Pick : LocationType.Bulk, l == 1 ? 600 : 1500, aisle, r.ToString("D2"), l.ToString("D2"), $"{aisle}{r}{l}");
        for (var i = 1; i <= 3; i++) Loc(zReceiving, $"REC-{i:D2}", LocationType.Floor, 2000);
        for (var i = 1; i <= 2; i++) Loc(zPacking, $"PACK-{i:D2}", LocationType.Floor, 500);
        for (var i = 1; i <= 3; i++) Loc(zShipping, $"SHIP-{i:D2}", LocationType.Floor, 2000);
        Loc(zReturns, "RET-01", LocationType.Floor, 500);
        Loc(zReturns, "DMG-01", LocationType.Damage, 500);
        Loc(eReceiving, "E-REC-01", LocationType.Floor, 2000);
        for (var i = 1; i <= 4; i++) Loc(eStorage, $"E-A-01-{i:D2}", LocationType.Pick, 800, "A", "01", i.ToString("D2"));
        db.Locations.AddRange(locs.Values);

        var uoms = new Dictionary<string, Uom>
        {
            ["PCS"] = new() { Id = Guid.NewGuid(), Code = "PCS", Name = "Piece", ConversionFactor = 1, IsBase = true },
        };
        foreach (var (code, name, factor) in new[] { ("BOX", "Box (12 pieces)", 12m), ("CTN", "Carton (24 pieces)", 24m), ("PLT", "Pallet (480 pieces)", 480m) })
            uoms[code] = new Uom { Id = Guid.NewGuid(), Code = code, Name = name, ConversionFactor = factor, BaseUomId = uoms["PCS"].Id, IsBase = false };
        db.Uoms.AddRange(uoms.Values);

        var food = new Category { Id = Guid.NewGuid(), Name = "Food", Level = 1 };
        var cats = new Dictionary<string, Category>
        {
            ["Dry Goods"] = new() { Id = Guid.NewGuid(), Name = "Dry Goods", ParentId = food.Id, Level = 2 },
            ["Dairy & Chilled"] = new() { Id = Guid.NewGuid(), Name = "Dairy & Chilled", ParentId = food.Id, Level = 2 },
            ["Beverages"] = new() { Id = Guid.NewGuid(), Name = "Beverages", Level = 1 },
            ["Snacks"] = new() { Id = Guid.NewGuid(), Name = "Snacks", Level = 1 },
            ["Household"] = new() { Id = Guid.NewGuid(), Name = "Household", Level = 1 },
            ["Personal Care"] = new() { Id = Guid.NewGuid(), Name = "Personal Care", Level = 1 },
        };
        db.Categories.Add(food);
        db.Categories.AddRange(cats.Values);

        var products = Skus.ToDictionary(s => s.Code, s => new Product
        {
            Id = Guid.NewGuid(), Sku = s.Code, Name = s.Name, CategoryId = cats[s.Cat].Id, UomId = uoms[s.Uom].Id,
            WeightKg = s.Kg, VolumeM3 = Math.Round(s.Kg / 400m, 4), Barcode = $"590{s.Code[^4..]}00001", MinStock = s.Min, MaxStock = s.Max, IsActive = true
        });
        db.Products.AddRange(products.Values);

        Supplier S(string code, string name, string contact, string phone) => new()
        { Id = Guid.NewGuid(), Code = code, Name = name, ContactPerson = contact, Email = $"orders@{code.ToLowerInvariant()}.example.com", Phone = phone, Address = "Sample Industrial Park", IsActive = true };
        var suppliers = new[]
        {
            S("SUP-GRN", "Greenleaf Staples Ltd", "Sam Sample", "+00 555 0101"), S("SUP-HVF", "Harvest Valley Foods", "Pat Placeholder", "+00 555 0102"),
            S("SUP-NFD", "Northfield Dairy Co-op", "Robin Roe", "+00 555 0103"), S("SUP-BLW", "Bluewave Beverages", "Casey Example", "+00 555 0104"),
            S("SUP-SNK", "Sunrise Snacks Ltd", "Jordan Doe", "+00 555 0105"), S("SUP-CLH", "CleanHome Supplies", "Taylor Test", "+00 555 0106"),
        }.ToDictionary(s => s.Code);
        db.Suppliers.AddRange(suppliers.Values);

        Carrier C(string name, string vehicle, string phone) => new()
        { Id = Guid.NewGuid(), Name = name, VehicleType = vehicle, ContactPerson = "Dispatch Desk", Phone = phone, Email = "dispatch@carrier.example.com", IsActive = true };
        var carriers = new[] { C("Swift Freight", "Box truck", "+00 555 0201"), C("ColdChain Express", "Refrigerated van", "+00 555 0202"), C("Metro Parcel", "Van", "+00 555 0203"), C("Harbor Haulage", "Semi-trailer", "+00 555 0204") };
        db.Carriers.AddRange(carriers);
        await db.SaveChangesAsync();

        // ---- inbound: initial stock arrives on six completed receipts (one per supplier)
        var n = 0;
        foreach (var group in Skus.GroupBy(s => s.Supplier))
        {
            n++;
            var r = await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers[group.Key].Id, main.Id, $"PO-{2400 + n}", $"ASN-{7100 + n}",
                today.AddDays(-n), $"DOOR-{n}", "Initial delivery", group.Select(s => new Inbound.AddReceiptItemDto(
                    products[s.Code].Id, s.Qty, s.Cost, s.ShelfDays.HasValue ? $"LOT-{s.Code[^4..]}" : null,
                    s.ShelfDays.HasValue ? today.AddDays(s.ShelfDays.Value) : null, locs[s.Bin].Id)).ToList()));
            foreach (var item in r.Items!)
                await inbound.ReceiveItemAsync(r.Id, new Inbound.ReceiveItemDto(item.Id, item.ExpectedQty, 0, item.PutawayLocationId, null, null), n % 2 == 0 ? supervisor : op);
            await inbound.CompleteReceiptAsync(r.Id, supervisor);
        }

        // one near-expiry lot so the dashboard has an expiry alert
        var yogurt = await db.Inventories.FirstAsync(i => i.ProductId == products["SKU-2002"].Id);
        yogurt.ExpiryDate = today.AddDays(5);
        await db.SaveChangesAsync();

        // partial delivery: shortage and damaged goods
        var partial = await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers["SUP-BLW"].Id, main.Id, "PO-2411", "ASN-7111", today, "DOOR-2", "Pallet damaged in transit",
        [
            new(products["SKU-3002"].Id, 120, 1.40m, "LOT-3002B", today.AddDays(150), locs["A-04-01"].Id),
            new(products["SKU-3003"].Id, 100, 1.60m, "LOT-3003B", today.AddDays(600), locs["A-04-02"].Id),
        ]));
        await inbound.ReceiveItemAsync(partial.Id, new Inbound.ReceiveItemDto(partial.Items![0].Id, 110, 6, locs["A-04-01"].Id, null, null), op);
        await inbound.ReceiveItemAsync(partial.Id, new Inbound.ReceiveItemDto(partial.Items![1].Id, 100, 0, locs["A-04-02"].Id, null, null), op);
        await inbound.CompleteReceiptAsync(partial.Id, supervisor);

        // in progress: one of two lines received
        var inProgress = await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers["SUP-SNK"].Id, main.Id, "PO-2412", "ASN-7112", today, "DOOR-3", "Unloading in progress",
        [
            new(products["SKU-4001"].Id, 200, 0.70m, null, today.AddDays(270), locs["B-04-03"].Id),
            new(products["SKU-4003"].Id, 150, 1.10m, null, today.AddDays(200), locs["B-04-03"].Id),
        ]));
        await inbound.ReceiveItemAsync(inProgress.Id, new Inbound.ReceiveItemDto(inProgress.Items![0].Id, 200, 0, locs["B-04-03"].Id, null, null), op);

        // planned deliveries (draft)
        await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers["SUP-NFD"].Id, main.Id, "PO-2413", "ASN-7113", today.AddDays(2), "DOOR-1", "Chilled delivery, book the cold dock",
        [
            new(products["SKU-2001"].Id, 300, 0.95m, "LOT-2001N", today.AddDays(120), locs["C-01-03"].Id),
            new(products["SKU-2004"].Id, 120, 3.30m, "LOT-2004N", today.AddDays(60), locs["C-02-03"].Id),
        ]));
        await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers["SUP-GRN"].Id, main.Id, "PO-2414", null, today.AddDays(5), null, "Restock of low sugar",
        [
            new(products["SKU-1004"].Id, 500, 1.05m, "LOT-1004B", today.AddDays(540), locs["A-04-02"].Id),
        ]));

        // second warehouse
        var eastReceipt = await inbound.CreateAsync(new Inbound.CreateInboundReceiptDto(suppliers["SUP-CLH"].Id, east.Id, "PO-2420", "ASN-7120", today.AddDays(-1), "E-DOOR-1", "Cross-dock stock",
        [
            new(products["SKU-5001"].Id, 80, 1.20m, null, null, locs["E-A-01-01"].Id),
            new(products["SKU-6002"].Id, 120, 0.85m, null, null, locs["E-A-01-02"].Id),
        ]));
        foreach (var item in eastReceipt.Items!)
            await inbound.ReceiveItemAsync(eastReceipt.Id, new Inbound.ReceiveItemDto(item.Id, item.ExpectedQty, 0, item.PutawayLocationId, null, null), op);
        await inbound.CompleteReceiptAsync(eastReceipt.Id, supervisor);

        // ---- outbound: one order in every state. A "customer" is the order destination.
        async Task<OutboundOrderResponseDto> NewOrder(string customer, Priority prio, OrderType type, int dueIn, string? notes, params (string Sku, int Qty)[] lines) =>
            await outbound.CreateAsync(new CreateOutboundOrderDto(main.Id, type, prio, customer, today, today.AddDays(dueIn), notes,
                lines.Select(l => new AddOrderItemDto(products[l.Sku].Id, l.Qty, Skus.First(s => s.Code == l.Sku).Price)).ToList()), supervisor);
        async Task Pick(Guid orderId, int? onlyFirst = null)
        {
            var tasks = (await outbound.GetPickTasksAsync(null, orderId, null)).OrderBy(t => t.ProductSku).ToList();
            foreach (var t in onlyFirst is null ? tasks : tasks.Take(onlyFirst.Value))
                await outbound.ConfirmPickAsync(t.Id, new ConfirmPickDto(t.QtyToPick), op);
        }
        async Task<ShipmentResponseDto> PackAndShip(Guid orderId, decimal kg, int carrier, string tracking)
        {
            await outbound.PackOrderAsync(orderId, new PackOrderDto($"CTN-{tracking[^4..]}", kg), op);
            return await outbound.ShipOrderAsync(orderId, new CreateShipmentDto(carriers[carrier].Id, tracking, $"BOL-{tracking[^4..]}", $"VH-{tracking[^3..]}", "Sample Driver"), op);
        }

        // delivered (Completed)
        var delivered = new List<OutboundOrderResponseDto>();
        var deliveredSpecs = new (string C, Priority P, (string, int)[] L, int Car)[]
        {
            ("Greenfield Supermarket #12", Priority.Normal, [("SKU-1001", 40), ("SKU-1002", 24), ("SKU-3001", 20)], 0),
            ("Corner Basket Mini-mart", Priority.Urgent, [("SKU-4001", 60), ("SKU-4003", 48)], 2),
            ("Riverside Hotel & Cafe", Priority.Normal, [("SKU-2001", 60), ("SKU-2003", 24), ("SKU-3004", 6)], 1),
            ("Lakeview Community School", Priority.Normal, [("SKU-1005", 50), ("SKU-4004", 30), ("SKU-5003", 24)], 0),
        };
        var t = 5000;
        foreach (var d in deliveredSpecs)
        {
            var o = await NewOrder(d.C, d.P, OrderType.Sales, -1, null, d.L);
            await outbound.StartPickingAsync(o.Id);
            await Pick(o.Id);
            var ship = await PackAndShip(o.Id, 25.5m, d.Car, $"TRK-{t++}");
            await outbound.MarkDeliveredAsync(ship.Id);
            delivered.Add(o);
        }

        // shipped, in transit
        foreach (var (c, lines, car) in new[]
        {
            ("Metro Wholesale Club", new (string, int)[] { ("SKU-1003", 30), ("SKU-5002", 20) }, 3),
            ("Sunny Day Convenience", new (string, int)[] { ("SKU-3002", 36), ("SKU-4002", 10) }, 2),
        })
        {
            var o = await NewOrder(c, Priority.Normal, OrderType.Sales, 1, null, lines);
            await outbound.StartPickingAsync(o.Id);
            await Pick(o.Id);
            await PackAndShip(o.Id, 31m, car, $"TRK-{t++}");
        }

        // packed, waiting for the carrier
        foreach (var (c, lines) in new[]
        {
            ("Old Town Grocers", new (string, int)[] { ("SKU-1001", 30), ("SKU-5001", 24) }),
            ("Hilltop Bistro", new (string, int)[] { ("SKU-2002", 20), ("SKU-2004", 6) }),
        })
        {
            var o = await NewOrder(c, c.StartsWith("Hill") ? Priority.Rush : Priority.Normal, OrderType.Sales, 1, null, lines);
            await outbound.StartPickingAsync(o.Id);
            await Pick(o.Id);
            await outbound.PackOrderAsync(o.Id, new PackOrderDto($"CTN-{t++}", 14.2m), op);
        }

        // picked, waiting to be packed
        foreach (var (c, lines) in new[]
        {
            ("Harbor View Hotel", new (string, int)[] { ("SKU-3003", 40), ("SKU-3001", 10) }),
            ("Maple Street Deli", new (string, int)[] { ("SKU-1006", 8), ("SKU-1002", 12) }),
        })
        {
            var o = await NewOrder(c, Priority.Normal, OrderType.Sales, 2, null, lines);
            await outbound.StartPickingAsync(o.Id);
            await Pick(o.Id);
        }

        // allocated, pick tasks open (one assigned to the operator, one half done)
        var allocA = await NewOrder("Westside Pharmacy", Priority.Urgent, OrderType.Sales, 1, "Deliver before noon", ("SKU-6001", 40), ("SKU-6002", 60));
        await outbound.StartPickingAsync(allocA.Id);
        await outbound.AssignPickerAsync(allocA.Id, new AssignPickerDto(op));
        var allocB = await NewOrder("Central Canteen", Priority.Normal, OrderType.Sales, 2, null, ("SKU-1001", 25), ("SKU-1005", 30), ("SKU-4003", 20));
        await outbound.StartPickingAsync(allocB.Id);
        await Pick(allocB.Id, onlyFirst: 1);

        // open (draft) orders
        await NewOrder("Brightside Kindergarten", Priority.Normal, OrderType.Sales, 4, "First order from this customer", ("SKU-3002", 24), ("SKU-4004", 12));
        await NewOrder("Pine Ridge Camp Store", Priority.Normal, OrderType.Sales, 5, null, ("SKU-1003", 10), ("SKU-5004", 30));
        await NewOrder("Marketing Samples", Priority.Normal, OrderType.Sample, 7, "Trade-show sample box", ("SKU-4002", 2), ("SKU-3003", 4));

        // cancelled after allocation: the reservation is released
        var cancelled = await NewOrder("Lakeside Cafe", Priority.Normal, OrderType.Sales, 1, "Cancelled by the customer", ("SKU-2001", 12));
        await outbound.StartPickingAsync(cancelled.Id);
        await outbound.CancelAsync(cancelled.Id);

        // ---- adjustments (the operator proposes, the supervisor decides)
        var invCount = (await db.Inventories.ToListAsync()).ToDictionary(i => (i.ProductId, i.LocationId), i => i.QtyOnHand);
        async Task<AdjustmentResponseDto> Adj(string sku, string bin, AdjType type, decimal qty, string reason) =>
            await inventory.CreateAdjustmentAsync(new CreateAdjustmentDto(products[sku].Id, locs[bin].Id, type, qty, reason), op);
        var damaged = await Adj("SKU-3002", "A-03-02", AdjType.Negative, 6, "Crushed cartons found during cycle count");
        await inventory.ApproveAdjustmentAsync(damaged.Id, supervisor, isAdmin: false);
        var found = await Adj("SKU-1005", "A-02-02", AdjType.Positive, 12, "Found a misplaced pack in aisle B");
        await inventory.ApproveAdjustmentAsync(found.Id, supervisor, isAdmin: false);
        var recount = await Adj("SKU-4001", "B-01-01", AdjType.Recount, invCount.GetValueOrDefault((products["SKU-4001"].Id, locs["B-01-01"].Id)) - 7, "Weekly cycle count: seven units missing");
        await inventory.ApproveAdjustmentAsync(recount.Id, supervisor, isAdmin: false);
        var rejected = await Adj("SKU-5002", "B-02-03", AdjType.Negative, 40, "Suspected theft (needs evidence)");
        await inventory.RejectAdjustmentAsync(rejected.Id, supervisor);
        await Adj("SKU-3004", "B-03-01", AdjType.Positive, 30, "Unrecorded supplier credit delivery");   // pending
        await Adj("SKU-2003", "C-02-01", AdjType.Negative, 4, "Fridge door left open, product discarded"); // pending

        // ---- transfers: replenishment moves
        var t1 = await transfers.CreateAsync(new CreateTransferDto(products["SKU-1001"].Id, locs["A-01-01"].Id, locs["A-04-03"].Id, 100, "Move overflow to bulk rack"), op);
        await transfers.CompleteAsync(t1.Id, op);
        var t2 = await transfers.CreateAsync(new CreateTransferDto(products["SKU-3001"].Id, locs["A-03-01"].Id, locs["A-04-03"].Id, 50, "Consolidate water cartons"), op);
        await transfers.CompleteAsync(t2.Id, op);
        await transfers.CreateAsync(new CreateTransferDto(products["SKU-2001"].Id, locs["C-01-01"].Id, locs["C-01-03"].Id, 80, "Free the pick face for tomorrow's delivery"), op);
        var t4 = await transfers.CreateAsync(new CreateTransferDto(products["SKU-4004"].Id, locs["B-02-01"].Id, locs["B-04-03"].Id, 20, "Cancelled: wrong bin"), op);
        await transfers.CancelAsync(t4.Id);

        // ---- returns from delivered orders
        async Task<ReturnResponseDto> Ret(int order, string sku, int qty, ReturnCondition cond, string reason) =>
            await returns.CreateAsync(new CreateReturnDto(main.Id, products[sku].Id, qty, cond, delivered[order].Id, reason, null), op);
        var good = await Ret(0, "SKU-1001", 4, ReturnCondition.Good, "Ordered the wrong size");
        await returns.ProcessAsync(good.Id, new ProcessReturnDto(Disposition.Restock, locs["A-01-01"].Id, "Packaging intact"), supervisor);
        var bad = await Ret(2, "SKU-2001", 6, ReturnCondition.Damaged, "Leaking cartons on arrival");
        await returns.ProcessAsync(bad.Id, new ProcessReturnDto(Disposition.Dispose, null, "Disposed with the chilled waste"), supervisor);
        var expired = await Ret(2, "SKU-3004", 2, ReturnCondition.Expired, "Customer reported past best-before date");
        await returns.ProcessAsync(expired.Id, new ProcessReturnDto(Disposition.WriteOff, null, null), supervisor);
        await Ret(1, "SKU-4001", 10, ReturnCondition.Good, "Store overstock, waiting for inspection");
        await Ret(3, "SKU-5003", 3, ReturnCondition.WrongItem, "Picked the wrong pack size");

        logger.LogInformation("Demo dataset ready: {Products} products, {Orders} orders, {Receipts} receipts",
            await db.Products.CountAsync(), await db.OutboundOrders.CountAsync(), await db.InboundReceipts.CountAsync());
    }
}
