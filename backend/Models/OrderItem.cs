using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("ordered_qty")]
    public decimal OrderedQty { get; set; }

    [Column("picked_qty")]
    public decimal PickedQty { get; set; }

    [Column("shipped_qty")]
    public decimal ShippedQty { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("status")]
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Pending;

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public OutboundOrder Order { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    public ICollection<PickTask> PickTasks { get; set; } = new List<PickTask>();
}
