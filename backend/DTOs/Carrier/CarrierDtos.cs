using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Carrier;

public record CreateCarrierDto(
    [Required] string Name,
    string? ContactPerson,
    string? Phone,
    [EmailAddress] string? Email,
    string? VehicleType
);

public record UpdateCarrierDto(
    string? Name,
    string? ContactPerson,
    string? Phone,
    [EmailAddress] string? Email,
    string? VehicleType,
    bool? IsActive
);

public record CarrierResponseDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? VehicleType,
    bool IsActive
);
