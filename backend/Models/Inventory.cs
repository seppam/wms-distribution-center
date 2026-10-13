using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("inventories")]
public class Inventory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("qty_on_hand")]
    public decimal QtyOnHand { get; set; }

    [Column("qty_reserved")]
    public decimal QtyReserved { get; set; }

    [MaxLength(100)]
    [Column("lot_number")]
    public string? LotNumber { get; set; }

    [Column("expiry_date")]
    public DateOnly? ExpiryDate { get; set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; }

    [Column("status")]
    public InventoryStatus Status { get; set; } = InventoryStatus.Available;

    [Column("last_counted_date")]
    public DateOnly? LastCountedDate { get; set; }

    /// <summary>Optimistic concurrency token, incremented on every change (prevents over-allocation under concurrency).</summary>
    [Column("version")]
    public int Version { get; set; }

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;
}
