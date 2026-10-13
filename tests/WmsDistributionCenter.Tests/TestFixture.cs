using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Tests;

/// <summary>The InMemory provider cannot map PostgreSQL jsonb (JsonDocument) columns, so ignore them in tests.</summary>
public sealed class TestDbContext : WmsDbContext
{
    public TestDbContext(DbContextOptions<WmsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Role>().Ignore(r => r.Permissions);
        modelBuilder.Entity<AuditLog>().Ignore(a => a.OldValues).Ignore(a => a.NewValues);
    }
}

/// <summary>Builds an isolated in-memory database with the minimum master data for the core flows.</summary>
public sealed class TestFixture : IDisposable
{
    public TestDbContext Db { get; }
    public Warehouse Warehouse { get; }
    public Supplier Supplier { get; }
    public Carrier Carrier { get; }
    public Product Product { get; }
    public Location Location { get; }
    public User User { get; }

    public TestFixture()
    {
        var options = new DbContextOptionsBuilder<WmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new TestDbContext(options);

        var role = new Role { Id = Guid.NewGuid(), Name = "Admin", IsSystem = true };
        User = new User { Id = Guid.NewGuid(), Username = "tester", Email = "tester@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret-demo", 4), FullName = "Test User", RoleId = role.Id, IsActive = true };
        Warehouse = new Warehouse { Id = Guid.NewGuid(), Code = "DC-T", Name = "Test DC", IsActive = true };
        var zone = new Zone { Id = Guid.NewGuid(), WarehouseId = Warehouse.Id, Name = "Storage", ZoneType = ZoneType.Storage, IsActive = true };
        Location = new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, LocationCode = "A-01-01", LocationType = LocationType.Pick, MaxCapacity = 1000, Status = LocationStatus.Empty };
        var uom = new Uom { Id = Guid.NewGuid(), Code = "PCS", Name = "Piece", ConversionFactor = 1, IsBase = true };
        Product = new Product { Id = Guid.NewGuid(), Sku = "SKU-T1", Name = "Test Product", UomId = uom.Id };
        Supplier = new Supplier { Id = Guid.NewGuid(), Code = "SUP-T", Name = "Test Supplier", IsActive = true };
        Carrier = new Carrier { Id = Guid.NewGuid(), Name = "Test Carrier", IsActive = true };

        Db.AddRange(role, User, Warehouse, zone, Location, uom, Product, Supplier, Carrier);
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}
