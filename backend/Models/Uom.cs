using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsDistributionCenter.Api.Models;

[Table("uoms")]
public class Uom
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("base_uom_id")]
    public Guid? BaseUomId { get; set; }

    [Column("conversion_factor")]
    public decimal ConversionFactor { get; set; } = 1m;

    [Column("is_base")]
    public bool IsBase { get; set; }

    // Navigation
    [ForeignKey(nameof(BaseUomId))]
    public Uom? BaseUom { get; set; }

    public ICollection<Uom> DerivedUoms { get; set; } = new List<Uom>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
