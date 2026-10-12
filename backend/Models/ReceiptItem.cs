using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("receipt_items")]
public class ReceiptItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("receipt_id")]
    public Guid ReceiptId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("expected_qty")]
    public decimal ExpectedQty { get; set; }

    [Column("received_qty")]
    public decimal ReceivedQty { get; set; }

    [Column("damaged_qty")]
    public decimal DamagedQty { get; set; }

    [MaxLength(100)]
    [Column("lot_number")]
    public string? LotNumber { get; set; }

    [Column("expiry_date")]
    public DateOnly? ExpiryDate { get; set; }

    [Column("unit_cost")]
    public decimal UnitCost { get; set; }

    [Column("status")]
    public ReceiptItemStatus Status { get; set; } = ReceiptItemStatus.Pending;

    [Column("putaway_location_id")]
    public Guid? PutawayLocationId { get; set; }

    // Navigation
    [ForeignKey(nameof(ReceiptId))]
    public InboundReceipt Receipt { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(PutawayLocationId))]
    public Location? PutawayLocation { get; set; }
}
