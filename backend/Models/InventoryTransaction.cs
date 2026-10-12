using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("inventory_transactions")]
public class InventoryTransaction
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("reference_type")]
    public ReferenceType ReferenceType { get; set; }

    [Column("reference_id")]
    public Guid ReferenceId { get; set; }

    [Column("transaction_type")]
    public TransactionType TransactionType { get; set; }

    [Column("qty_before")]
    public decimal QtyBefore { get; set; }

    [Column("qty_after")]
    public decimal QtyAfter { get; set; }

    [Column("uom_qty")]
    public decimal UomQty { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [MaxLength(100)]
    [Column("lot_number")]
    public string? LotNumber { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
