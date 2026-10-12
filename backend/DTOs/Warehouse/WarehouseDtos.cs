using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Warehouse;

public record CreateWarehouseDto(
    [Required] string Code,
    [Required] string Name,
    string? Address,
    string? City,
    decimal Capacity
);

public record UpdateWarehouseDto(
    string? Name,
    string? Address,
    string? City,
    decimal? Capacity,
    bool? IsActive
);

public record WarehouseResponseDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    string? City,
    decimal Capacity,
    bool IsActive,
    DateTime CreatedAt
);
