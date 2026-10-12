using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("inbound_receipts")]
public class InboundReceipt
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("receipt_no")]
    public string ReceiptNo { get; set; } = string.Empty;

    [Column("supplier_id")]
    public Guid SupplierId { get; set; }

    [Column("warehouse_id")]
    public Guid WarehouseId { get; set; }

    [MaxLength(100)]
    [Column("po_number")]
    public string? PoNumber { get; set; }

    [MaxLength(100)]
    [Column("asn_number")]
    public string? AsnNumber { get; set; }

    [Column("status")]
    public InboundStatus Status { get; set; } = InboundStatus.Draft;

    [Column("expected_date")]
    public DateOnly? ExpectedDate { get; set; }

    [Column("received_date")]
    public DateTime? ReceivedDate { get; set; }

    [Column("received_by")]
    public Guid? ReceivedBy { get; set; }

    [MaxLength(50)]
    [Column("dock_door")]
    public string? DockDoor { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SupplierId))]
    public Supplier Supplier { get; set; } = null!;

    [ForeignKey(nameof(WarehouseId))]
    public Warehouse Warehouse { get; set; } = null!;

    [ForeignKey(nameof(ReceivedBy))]
    public User? Receiver { get; set; }

    public ICollection<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
}
