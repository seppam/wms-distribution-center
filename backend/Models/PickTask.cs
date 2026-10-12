using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("pick_tasks")]
public class PickTask
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_item_id")]
    public Guid OrderItemId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("from_location_id")]
    public Guid FromLocationId { get; set; }

    [Column("qty_to_pick")]
    public decimal QtyToPick { get; set; }

    [Column("qty_picked")]
    public decimal QtyPicked { get; set; }

    [Column("status")]
    public PickTaskStatus Status { get; set; } = PickTaskStatus.Assigned;

    [Column("picker_id")]
    public Guid? PickerId { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("picked_at")]
    public DateTime? PickedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderItemId))]
    public OrderItem OrderItem { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(FromLocationId))]
    public Location FromLocation { get; set; } = null!;

    [ForeignKey(nameof(PickerId))]
    public User? Picker { get; set; }
}
