using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(WmsDbContext db, bool demoData = false)
    {
        // Ensure DB is created
        await db.Database.EnsureCreatedAsync();

        // Seed roles
        if (!await db.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "Full system access", IsSystem = true, CreatedAt = DateTime.UtcNow },
                new Role { Id = Guid.NewGuid(), Name = "Supervisor", Description = "Warehouse supervisor", IsSystem = true, CreatedAt = DateTime.UtcNow },
                new Role { Id = Guid.NewGuid(), Name = "Operator", Description = "Warehouse operator", IsSystem = true, CreatedAt = DateTime.UtcNow },
                new Role { Id = Guid.NewGuid(), Name = "Viewer", Description = "Read-only access", IsSystem = true, CreatedAt = DateTime.UtcNow }
            };
            db.Roles.AddRange(roles);
            await db.SaveChangesAsync();
        }

        // Seed admin user
        if (!await db.Users.AnyAsync())
        {
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                FullName = "System Administrator",
                RoleId = adminRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        if (demoData) await SeedDemoDataAsync(db);
    }

    /// <summary>Fictional demo master data and stock so the UI is not empty. Safe to skip in production.</summary>
    private static async Task SeedDemoDataAsync(WmsDbContext db)
    {
        if (await db.Warehouses.AnyAsync()) return;

        var wh = new Warehouse { Id = Guid.NewGuid(), Code = "DC-MAIN", Name = "Main Distribution Center", City = "Demo City", Address = "1 Example Road", Capacity = 50000, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Warehouses.Add(wh);

        var zones = new[]
        {
            new Zone { Id = Guid.NewGuid(), WarehouseId = wh.Id, Name = "Receiving Dock", ZoneType = ZoneType.Receiving, IsActive = true },
            new Zone { Id = Guid.NewGuid(), WarehouseId = wh.Id, Name = "Ambient Storage", ZoneType = ZoneType.Storage, IsActive = true },
            new Zone { Id = Guid.NewGuid(), WarehouseId = wh.Id, Name = "Chilled Storage", ZoneType = ZoneType.Storage, IsActive = true },
            new Zone { Id = Guid.NewGuid(), WarehouseId = wh.Id, Name = "Packing Area", ZoneType = ZoneType.Packing, IsActive = true },
            new Zone { Id = Guid.NewGuid(), WarehouseId = wh.Id, Name = "Shipping Dock", ZoneType = ZoneType.Shipping, IsActive = true }
        };
        db.Zones.AddRange(zones);

        var locations = new List<Location>();
        foreach (var (zone, prefix) in new[] { (zones[1], "A"), (zones[2], "C") })
            for (var rack = 1; rack <= 3; rack++)
                for (var level = 1; level <= 2; level++)
                    locations.Add(new Location
                    {
                        Id = Guid.NewGuid(), ZoneId = zone.Id,
                        LocationCode = $"{prefix}-{rack:D2}-{level:D2}", Aisle = prefix, Rack = rack.ToString("D2"), Level = level.ToString("D2"),
                        LocationType = LocationType.Pick, MaxCapacity = 1000, Status = LocationStatus.Empty
                    });
        db.Locations.AddRange(locations);

        var uom = new Uom { Id = Guid.NewGuid(), Code = "PCS", Name = "Piece", ConversionFactor = 1, IsBase = true };
        db.Uoms.Add(uom);
        var catFood = new Category { Id = Guid.NewGuid(), Name = "Dry Goods", Level = 1 };
        var catDairy = new Category { Id = Guid.NewGuid(), Name = "Dairy", Level = 1 };
        db.Categories.AddRange(catFood, catDairy);

        var products = new[]
        {
            new Product { Id = Guid.NewGuid(), Sku = "SKU-1001", Name = "Long Grain Rice 5kg", CategoryId = catFood.Id, UomId = uom.Id, WeightKg = 5, MinStock = 50 },
            new Product { Id = Guid.NewGuid(), Sku = "SKU-1002", Name = "Cooking Oil 2L", CategoryId = catFood.Id, UomId = uom.Id, WeightKg = 2, MinStock = 40 },
            new Product { Id = Guid.NewGuid(), Sku = "SKU-1003", Name = "Instant Noodles Pack", CategoryId = catFood.Id, UomId = uom.Id, WeightKg = 0.1m, MinStock = 200 },
            new Product { Id = Guid.NewGuid(), Sku = "SKU-2001", Name = "UHT Milk 1L", CategoryId = catDairy.Id, UomId = uom.Id, WeightKg = 1, MinStock = 100 },
            new Product { Id = Guid.NewGuid(), Sku = "SKU-2002", Name = "Plain Yogurt 500g", CategoryId = catDairy.Id, UomId = uom.Id, WeightKg = 0.5m, MinStock = 60 }
        };
        db.Products.AddRange(products);

        db.Suppliers.AddRange(
            new Supplier { Id = Guid.NewGuid(), Code = "SUP-001", Name = "Demo Foods Ltd", ContactPerson = "Sample Contact", IsActive = true },
            new Supplier { Id = Guid.NewGuid(), Code = "SUP-002", Name = "Sample Dairy Co", ContactPerson = "Sample Contact", IsActive = true });
        db.Carriers.AddRange(
            new Carrier { Id = Guid.NewGuid(), Name = "Demo Logistics", VehicleType = "Box truck", IsActive = true },
            new Carrier { Id = Guid.NewGuid(), Name = "Sample Freight", VehicleType = "Refrigerated van", IsActive = true });

        var rng = new Random(42);
        for (var i = 0; i < products.Length; i++)
        {
            var loc = locations[i];
            loc.Status = LocationStatus.Occupied;
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(), ProductId = products[i].Id, LocationId = loc.Id,
                QtyOnHand = 100 + rng.Next(0, 400), QtyReserved = 0, UnitCost = 1 + i, Status = InventoryStatus.Available
            });
        }

        await db.SaveChangesAsync();
    }
}
