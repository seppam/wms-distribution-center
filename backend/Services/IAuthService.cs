using WmsDistributionCenter.Api.DTOs.Auth;

namespace WmsDistributionCenter.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse?> RegisterAsync(RegisterRequest request);
    Task<UserInfo?> GetCurrentUserAsync(Guid userId);
}
