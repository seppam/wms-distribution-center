using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Role;

public record CreateRoleDto(
    [Required] string Name,
    string? Description,
    string? Permissions,
    bool IsSystem
);

public record UpdateRoleDto(
    string? Name,
    string? Description,
    string? Permissions,
    bool? IsSystem
);

public record RoleResponseDto(
    Guid Id,
    string Name,
    string? Description,
    string? Permissions,
    bool IsSystem,
    DateTime CreatedAt
);
