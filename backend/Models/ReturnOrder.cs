using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("return_orders")]
public class ReturnOrder
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("return_no")]
    public string ReturnNo { get; set; } = string.Empty;

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("warehouse_id")]
    public Guid WarehouseId { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("condition")]
    public ReturnCondition Condition { get; set; }

    [Column("disposition")]
    public Disposition Disposition { get; set; }

    [Column("status")]
    public ReturnStatus Status { get; set; } = ReturnStatus.Received;

    [Column("received_by")]
    public Guid? ReceivedBy { get; set; }

    [Column("received_at")]
    public DateTime? ReceivedAt { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public OutboundOrder? Order { get; set; }

    [ForeignKey(nameof(WarehouseId))]
    public Warehouse Warehouse { get; set; } = null!;

    [ForeignKey(nameof(ReceivedBy))]
    public User? Receiver { get; set; }
}
