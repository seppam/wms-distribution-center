using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("adjustments")]
public class Adjustment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("adj_number")]
    public string AdjNumber { get; set; } = string.Empty;

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Column("adj_type")]
    public AdjType AdjType { get; set; }

    [Column("qty_before")]
    public decimal QtyBefore { get; set; }

    [Column("adj_qty")]
    public decimal AdjQty { get; set; }

    [Column("qty_after")]
    public decimal QtyAfter { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("status")]
    public AdjStatus Status { get; set; } = AdjStatus.Draft;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public Location Location { get; set; } = null!;

    [ForeignKey(nameof(ApprovedBy))]
    public User? Approver { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User Creator { get; set; } = null!;
}
