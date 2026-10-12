using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.Zone;

public record CreateZoneDto(
    [Required] Guid WarehouseId,
    [Required] string Name,
    ZoneType ZoneType,
    string? Description
);

public record UpdateZoneDto(
    string? Name,
    ZoneType? ZoneType,
    string? Description,
    bool? IsActive
);

public record ZoneResponseDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    string Name,
    ZoneType ZoneType,
    string? Description,
    bool IsActive
);
