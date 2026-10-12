using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsDistributionCenter.Api.Models;

[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("uom_id")]
    public Guid UomId { get; set; }

    [Column("weight_kg")]
    public decimal WeightKg { get; set; }

    [Column("volume_m3")]
    public decimal VolumeM3 { get; set; }

    [MaxLength(255)]
    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("min_stock")]
    public int MinStock { get; set; }

    [Column("max_stock")]
    public int? MaxStock { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }

    [ForeignKey(nameof(UomId))]
    public Uom Uom { get; set; } = null!;

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
