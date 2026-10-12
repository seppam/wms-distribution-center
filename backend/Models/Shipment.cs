using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("shipments")]
public class Shipment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("carrier_id")]
    public Guid CarrierId { get; set; }

    [MaxLength(100)]
    [Column("tracking_no")]
    public string? TrackingNo { get; set; }

    [MaxLength(100)]
    [Column("bol_number")]
    public string? BolNumber { get; set; }

    [MaxLength(50)]
    [Column("vehicle_plate")]
    public string? VehiclePlate { get; set; }

    [MaxLength(255)]
    [Column("driver_name")]
    public string? DriverName { get; set; }

    [Column("status")]
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Draft;

    [Column("shipped_at")]
    public DateTime? ShippedAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    // Navigation
    [ForeignKey(nameof(OrderId))]
    public OutboundOrder Order { get; set; } = null!;

    [ForeignKey(nameof(CarrierId))]
    public Carrier Carrier { get; set; } = null!;
}
