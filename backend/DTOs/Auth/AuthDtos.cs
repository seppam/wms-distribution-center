using System.ComponentModel.DataAnnotations;

namespace WmsDistributionCenter.Api.DTOs.Auth;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserInfo User
);

public record RegisterRequest(
    [Required][StringLength(100, MinimumLength = 3)] string Username,
    [Required][EmailAddress] string Email,
    [Required][MinLength(10)] string Password,
    [Required] string FullName,
    Guid? RoleId,
    string? Phone,
    Guid? WarehouseId
);

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string? Warehouse,
    string? Phone
);
