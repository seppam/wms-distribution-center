using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Category;

public record CreateCategoryDto(
    [Required] string Name,
    Guid? ParentId,
    string? Description
);

public record UpdateCategoryDto(
    string? Name,
    Guid? ParentId,
    string? Description
);

public record CategoryResponseDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ParentName,
    int Level,
    string? Description
);

public record CategoryTreeDto(
    Guid Id,
    string Name,
    int Level,
    string? Description,
    List<CategoryTreeDto> Children
);
