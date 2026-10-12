using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Models;

[Table("locations")]
public class Location
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("zone_id")]
    public Guid ZoneId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("location_code")]
    public string LocationCode { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("aisle")]
    public string? Aisle { get; set; }

    [MaxLength(20)]
    [Column("rack")]
    public string? Rack { get; set; }

    [MaxLength(20)]
    [Column("level")]
    public string? Level { get; set; }

    [MaxLength(20)]
    [Column("bin")]
    public string? Bin { get; set; }

    [Column("location_type")]
    public LocationType LocationType { get; set; }

    [Column("max_capacity")]
    public decimal MaxCapacity { get; set; }

    [Column("status")]
    public LocationStatus Status { get; set; } = LocationStatus.Empty;

    // Navigation
    [ForeignKey(nameof(ZoneId))]
    public Zone Zone { get; set; } = null!;

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
