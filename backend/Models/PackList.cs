using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("pack_lists")]
public class PackList
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("carton_no")]
    public string CartonNo { get; set; } = string.Empty;

    [Column("packed_by")]
    public Guid PackedBy { get; set; }

    [Column("weight_kg")]
    public decimal WeightKg { get; set; }

    [Column("packed_at")]
    public DateTime? PackedAt { get; set; }

    [Column("status")]
    public PackListStatus Status { get; set; } = PackListStatus.Open;

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public OutboundOrder Order { get; set; } = null!;

    [ForeignKey(nameof(PackedBy))]
    public User Packer { get; set; } = null!;
}
