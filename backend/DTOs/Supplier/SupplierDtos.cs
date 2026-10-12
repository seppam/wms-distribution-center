using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Supplier;

public record CreateSupplierDto(
    [Required] string Code,
    [Required] string Name,
    string? ContactPerson,
    [EmailAddress] string? Email,
    string? Phone,
    string? Address
);

public record UpdateSupplierDto(
    string? Name,
    string? ContactPerson,
    [EmailAddress] string? Email,
    string? Phone,
    string? Address,
    bool? IsActive
);

public record SupplierResponseDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive
);
