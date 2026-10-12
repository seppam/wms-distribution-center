using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.Location;

public record CreateLocationDto(
    [Required] Guid ZoneId,
    [Required] string LocationCode,
    string? Aisle,
    string? Rack,
    string? Level,
    string? Bin,
    LocationType LocationType,
    decimal MaxCapacity
);

public record UpdateLocationDto(
    string? LocationCode,
    string? Aisle,
    string? Rack,
    string? Level,
    string? Bin,
    LocationType? LocationType,
    decimal? MaxCapacity,
    LocationStatus? Status
);

public record LocationResponseDto(
    Guid Id,
    Guid ZoneId,
    string ZoneName,
    string WarehouseName,
    string LocationCode,
    string? Aisle,
    string? Rack,
    string? Level,
    string? Bin,
    LocationType LocationType,
    decimal MaxCapacity,
    LocationStatus Status
);
