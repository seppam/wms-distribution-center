using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Product;

public record CreateProductDto(
    [Required] string Sku,
    [Required] string Name,
    Guid? CategoryId,
    [Required] Guid UomId,
    decimal WeightKg,
    decimal VolumeM3,
    string? Barcode,
    int MinStock,
    int? MaxStock
);

public record UpdateProductDto(
    string? Name,
    Guid? CategoryId,
    Guid? UomId,
    decimal? WeightKg,
    decimal? VolumeM3,
    string? Barcode,
    int? MinStock,
    int? MaxStock,
    bool? IsActive
);

public record ProductResponseDto(
    Guid Id,
    string Sku,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    Guid UomId,
    string UomName,
    decimal WeightKg,
    decimal VolumeM3,
    string? Barcode,
    int MinStock,
    int? MaxStock,
    bool IsActive
);
