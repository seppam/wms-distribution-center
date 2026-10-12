using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("transfers")]
public class Transfer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("transfer_no")]
    public string TransferNo { get; set; } = string.Empty;

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("from_location_id")]
    public Guid FromLocationId { get; set; }

    [Column("to_location_id")]
    public Guid ToLocationId { get; set; }

    [Column("qty")]
    public decimal Qty { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("requested_by")]
    public Guid RequestedBy { get; set; }

    [Column("status")]
    public TransferStatus Status { get; set; } = TransferStatus.Draft;

    [Column("transferred_at")]
    public DateTime? TransferredAt { get; set; }

    // Navigation
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(FromLocationId))]
    public Location FromLocation { get; set; } = null!;

    [ForeignKey(nameof(ToLocationId))]
    public Location ToLocation { get; set; } = null!;

    [ForeignKey(nameof(RequestedBy))]
    public User Requester { get; set; } = null!;
}
