using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Uom;

public record CreateUomDto(
    [Required] string Code,
    [Required] string Name,
    Guid? BaseUomId,
    decimal ConversionFactor,
    bool IsBase
);

public record UpdateUomDto(
    string? Name,
    Guid? BaseUomId,
    decimal? ConversionFactor,
    bool? IsBase
);

public record UomResponseDto(
    Guid Id,
    string Code,
    string Name,
    Guid? BaseUomId,
    string? BaseUomName,
    decimal ConversionFactor,
    bool IsBase
);
