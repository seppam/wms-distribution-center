using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.User;

public record CreateUserDto(
    [Required] string Username,
    [Required][EmailAddress] string Email,
    [Required][MinLength(10)] string Password,
    [Required] string FullName,
    [Required] Guid RoleId,
    Guid? WarehouseId,
    string? Phone
);

public record UpdateUserDto(
    [EmailAddress] string? Email,
    [MinLength(10)] string? Password,
    string? FullName,
    Guid? RoleId,
    Guid? WarehouseId,
    string? Phone,
    bool? IsActive
);

public record UserResponseDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    Guid RoleId,
    string RoleName,
    Guid? WarehouseId,
    string? WarehouseName,
    string? Phone,
    bool IsActive,
    DateTime? LastLogin,
    DateTime CreatedAt
);
