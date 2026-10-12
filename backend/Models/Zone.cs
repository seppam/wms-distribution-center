using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("zones")]
public class Zone
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("warehouse_id")]
    public Guid WarehouseId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("zone_type")]
    public ZoneType ZoneType { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(WarehouseId))]
    public Warehouse Warehouse { get; set; } = null!;

    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
