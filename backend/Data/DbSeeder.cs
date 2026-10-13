using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Security;

namespace WmsDistributionCenter.Api.Data;

/// <summary>Seeding options. Demo credentials are only ever created when running in Development.</summary>
public sealed record SeedOptions(
    bool DemoData = false,
    bool IsDevelopment = false,
    string? DemoPassword = null,
    string? AdminUsername = null,
    string? AdminEmail = null,
    string? AdminPassword = null);

public static class DbSeeder
{
    /// <summary>Documented, throwaway password for the Development-only demo accounts.</summary>
    public const string DefaultDemoPassword = "Demo!Pass123";

    public static async Task SeedAsync(WmsDbContext db, SeedOptions options, ILogger? logger = null)
    {
        await EnsureRolesAsync(db);

        if (!await db.Users.AnyAsync())
        {
            var roles = await db.Roles.ToDictionaryAsync(r => r.Name);
            if (options.DemoData && options.IsDevelopment)
            {
                var password = string.IsNullOrWhiteSpace(options.DemoPassword) ? DefaultDemoPassword : options.DemoPassword;
                foreach (var (username, role, fullName) in new[]
                {
                    ("admin", AppRoles.Admin, "Demo Administrator"),
                    ("supervisor", AppRoles.Supervisor, "Demo Supervisor"),
                    ("operator", AppRoles.Operator, "Demo Operator"),
                    ("viewer", AppRoles.Viewer, "Demo Viewer")
                })
                    db.Users.Add(NewUser(username, $"{username}@example.com", fullName, password, roles[role].Id));
                await db.SaveChangesAsync();
                logger?.LogWarning("Seeded Development demo accounts (admin, supervisor, operator, viewer). Never enable this outside local development.");
            }
            else if (!string.IsNullOrWhiteSpace(options.AdminPassword))
            {
                if (options.AdminPassword.Length < 12)
                    throw new InvalidOperationException("Seed:AdminPassword must be at least 12 characters.");
                var username = string.IsNullOrWhiteSpace(options.AdminUsername) ? "admin" : options.AdminUsername;
                var email = string.IsNullOrWhiteSpace(options.AdminEmail) ? $"{username}@example.com" : options.AdminEmail;
                db.Users.Add(NewUser(username, email, "Administrator", options.AdminPassword, roles[AppRoles.Admin].Id));
                await db.SaveChangesAsync();
                logger?.LogInformation("Created the bootstrap administrator '{User}' from configuration.", username);
            }
            else
            {
                logger?.LogWarning("No users exist. Set Seed__AdminPassword (12+ chars) to create the first administrator.");
            }
        }

        if (options.DemoData) await SeedDemoDataAsync(db);
    }

    /// <summary>Seeds the four built-in roles when the table is empty (always required).</summary>
    public static async Task EnsureRolesAsync(WmsDbContext db)
    {
        if (await db.Roles.AnyAsync()) return;
        db.Roles.AddRange(
            new Role { Id = Guid.NewGuid(), Name = AppRoles.Admin, Description = "Full system access, user and role administration", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid(), Name = AppRoles.Supervisor, Description = "Plans work, manages master data, approves adjustments", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid(), Name = AppRoles.Operator, Description = "Floor work: receive, pick, pack, ship, count, transfer", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid(), Name = AppRoles.Viewer, Description = "Read-only access", IsSystem = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    internal static User NewUser(string username, string email, string fullName, string password, Guid roleId) => new()
    {
        Id = Guid.NewGuid(), Username = username, Email = email, FullName = fullName,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), RoleId = roleId, IsActive = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

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
