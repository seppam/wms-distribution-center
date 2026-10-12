using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("outbound_orders")]
public class OutboundOrder
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("warehouse_id")]
    public Guid WarehouseId { get; set; }

    [Column("order_type")]
    public OrderType OrderType { get; set; }

    [Column("status")]
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    [Column("priority")]
    public Priority Priority { get; set; } = Priority.Normal;

    [Column("destination")]
    public string? Destination { get; set; }

    [Column("requested_date")]
    public DateOnly? RequestedDate { get; set; }

    [Column("due_date")]
    public DateOnly? DueDate { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(WarehouseId))]
    public Warehouse Warehouse { get; set; } = null!;

    [ForeignKey(nameof(CreatedBy))]
    public User Creator { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<PackList> PackLists { get; set; } = new List<PackList>();
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<ReturnOrder> Returns { get; set; } = new List<ReturnOrder>();
}
