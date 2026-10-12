using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsDistributionCenter.Api.Models;

[Table("categories")]
public class Category
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    [Column("level")]
    public int Level { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    // Navigation
    [ForeignKey(nameof(ParentId))]
    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
