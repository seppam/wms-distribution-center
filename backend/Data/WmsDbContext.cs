using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Data;

public class WmsDbContext : DbContext
{
    public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Uom> Uoms => Set<Uom>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InboundReceipt> InboundReceipts => Set<InboundReceipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<OutboundOrder> OutboundOrders => Set<OutboundOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PickTask> PickTasks => Set<PickTask>();
    public DbSet<PackList> PackLists => Set<PackList>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ReturnOrder> ReturnOrders => Set<ReturnOrder>();
    public DbSet<Adjustment> Adjustments => Set<Adjustment>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.Warehouse).WithMany(w => w.Users).HasForeignKey(u => u.WarehouseId).OnDelete(DeleteBehavior.SetNull);
        });

        // Warehouse
        modelBuilder.Entity<Warehouse>(e =>
        {
            e.HasIndex(w => w.Code).IsUnique();
        });

        // Zone
        modelBuilder.Entity<Zone>(e =>
        {
            e.HasOne(z => z.Warehouse).WithMany(w => w.Zones).HasForeignKey(z => z.WarehouseId).OnDelete(DeleteBehavior.Cascade);
        });

        // Location
        modelBuilder.Entity<Location>(e =>
        {
            e.HasIndex(l => l.LocationCode).IsUnique();
            e.HasOne(l => l.Zone).WithMany(z => z.Locations).HasForeignKey(l => l.ZoneId).OnDelete(DeleteBehavior.Cascade);
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // Uom
        modelBuilder.Entity<Uom>(e =>
        {
            e.HasIndex(u => u.Code).IsUnique();
            e.HasOne(u => u.BaseUom).WithMany(u => u.DerivedUoms).HasForeignKey(u => u.BaseUomId).OnDelete(DeleteBehavior.Restrict);
        });

        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.Uom).WithMany(u => u.Products).HasForeignKey(p => p.UomId).OnDelete(DeleteBehavior.Restrict);
        });

        // Supplier
        modelBuilder.Entity<Supplier>(e =>
        {
            e.HasIndex(s => s.Code).IsUnique();
        });

        // Carrier
        // (no unique field)

        // Inventory — composite unique index on ProductId + LocationId
        modelBuilder.Entity<Inventory>(e =>
        {
            e.HasIndex(i => new { i.ProductId, i.LocationId }).IsUnique();
            e.HasOne(i => i.Product).WithMany(p => p.Inventories).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Location).WithMany(l => l.Inventories).HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryTransaction
        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.HasOne(it => it.Product).WithMany().HasForeignKey(it => it.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(it => it.Location).WithMany().HasForeignKey(it => it.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(it => it.User).WithMany(u => u.InventoryTransactions).HasForeignKey(it => it.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // InboundReceipt
        modelBuilder.Entity<InboundReceipt>(e =>
        {
            e.HasIndex(r => r.ReceiptNo).IsUnique();
            e.HasOne(r => r.Supplier).WithMany(s => s.InboundReceipts).HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Warehouse).WithMany().HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Receiver).WithMany().HasForeignKey(r => r.ReceivedBy).OnDelete(DeleteBehavior.SetNull);
        });

        // ReceiptItem
        modelBuilder.Entity<ReceiptItem>(e =>
        {
            e.HasOne(ri => ri.Receipt).WithMany(r => r.Items).HasForeignKey(ri => ri.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ri => ri.Product).WithMany().HasForeignKey(ri => ri.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ri => ri.PutawayLocation).WithMany().HasForeignKey(ri => ri.PutawayLocationId).OnDelete(DeleteBehavior.SetNull);
        });

        // OutboundOrder
        modelBuilder.Entity<OutboundOrder>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasOne(o => o.Warehouse).WithMany().HasForeignKey(o => o.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Creator).WithMany().HasForeignKey(o => o.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasOne(oi => oi.Order).WithMany(o => o.Items).HasForeignKey(oi => oi.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(oi => oi.Product).WithMany().HasForeignKey(oi => oi.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // PickTask
        modelBuilder.Entity<PickTask>(e =>
        {
            e.HasOne(pt => pt.OrderItem).WithMany(oi => oi.PickTasks).HasForeignKey(pt => pt.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pt => pt.Product).WithMany().HasForeignKey(pt => pt.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(pt => pt.FromLocation).WithMany().HasForeignKey(pt => pt.FromLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(pt => pt.Picker).WithMany(u => u.PickTasks).HasForeignKey(pt => pt.PickerId).OnDelete(DeleteBehavior.SetNull);
        });

        // PackList
        modelBuilder.Entity<PackList>(e =>
        {
            e.HasOne(pl => pl.Order).WithMany(o => o.PackLists).HasForeignKey(pl => pl.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pl => pl.Packer).WithMany(u => u.PackLists).HasForeignKey(pl => pl.PackedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // Shipment
        modelBuilder.Entity<Shipment>(e =>
        {
            e.HasOne(s => s.Order).WithMany(o => o.Shipments).HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Carrier).WithMany(c => c.Shipments).HasForeignKey(s => s.CarrierId).OnDelete(DeleteBehavior.Restrict);
        });

        // ReturnOrder
        modelBuilder.Entity<ReturnOrder>(e =>
        {
            e.HasIndex(r => r.ReturnNo).IsUnique();
            e.HasOne(r => r.Order).WithMany(o => o.Returns).HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Warehouse).WithMany().HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Receiver).WithMany().HasForeignKey(r => r.ReceivedBy).OnDelete(DeleteBehavior.SetNull);
        });

        // Adjustment
        modelBuilder.Entity<Adjustment>(e =>
        {
            e.HasIndex(a => a.AdjNumber).IsUnique();
            e.HasOne(a => a.Product).WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Location).WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Approver).WithMany().HasForeignKey(a => a.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.Creator).WithMany().HasForeignKey(a => a.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // Transfer
        modelBuilder.Entity<Transfer>(e =>
        {
            e.HasIndex(t => t.TransferNo).IsUnique();
            e.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.FromLocation).WithMany().HasForeignKey(t => t.FromLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToLocation).WithMany().HasForeignKey(t => t.ToLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Requester).WithMany().HasForeignKey(t => t.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasOne(al => al.User).WithMany().HasForeignKey(al => al.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
