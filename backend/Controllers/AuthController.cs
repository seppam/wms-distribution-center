using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WmsDistributionCenter.Api.DTOs.Auth;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "Invalid username or password" });
        return Ok(result);
    }

    /// <summary>Public self-registration (least-privileged Viewer role). Disabled unless Auth:AllowSelfRegistration is true.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!_config.GetValue<bool>("Auth:AllowSelfRegistration"))
            return NotFound(new { message = "Self-registration is disabled. Ask an administrator for an account." });
        var result = await _authService.RegisterAsync(request);
        if (result == null)
            return BadRequest(new { message = "Username or email already exists" });
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _authService.GetCurrentUserAsync(Guid.Parse(userId));
        if (user == null) return NotFound();
        return Ok(user);
    }
}
