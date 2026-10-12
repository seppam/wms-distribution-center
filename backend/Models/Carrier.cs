using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsDistributionCenter.Api.Models;

[Table("carriers")]
public class Carrier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(100)]
    [Column("vehicle_type")]
    public string? VehicleType { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
}
