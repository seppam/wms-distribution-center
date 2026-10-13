using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Demo;
using WmsDistributionCenter.Api.Security;

namespace WmsDistributionCenter.Api.Controllers;

/// <summary>Public demo mode support. Everything here is inert unless Demo:Enabled is true.</summary>
[ApiController]
[Route("api")]
public class DemoController(DemoOptions options, DemoResetThrottle throttle, DemoDataSeeder seeder) : ControllerBase
{
    /// <summary>
    /// Tells the web app whether this is a public demo instance. Anonymous by design and deliberately minimal:
    /// a flag and the role names, never a password or any account detail beyond the conventional user name.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("demo-info")]
    public IActionResult Info() =>
        options.Enabled
            ? Ok(new { enabled = true, accounts = DemoDataSeeder.Accounts.Select(a => new { role = a.Role, username = a.Username }) })
            : Ok(new { enabled = false });

    /// <summary>Restores the seeded dataset. Admin only, only in demo mode (404 otherwise), and rate limited.</summary>
    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPost("demo/reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        if (!options.Enabled) return NotFound(new { message = "Demo mode is not enabled." });
        if (!throttle.TryAcquire(out var retryAfter))
        {
            Response.Headers.RetryAfter = retryAfter.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = $"The demo was reset too often. Try again in {retryAfter} seconds." });
        }
        await seeder.ResetAsync(ct);
        return Ok(new { message = "Demo data restored." });
    }
}
