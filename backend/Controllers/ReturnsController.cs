using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;
 using WmsDistributionCenter.Api.DTOs.Return;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _returns;

    public ReturnsController(IReturnService returns) => _returns = returns;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _returns.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var r = await _returns.GetByIdAsync(id);
        return r == null ? NotFound() : Ok(r);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var r = await _returns.CreateAsync(dto, Guid.Parse(userId));
        return CreatedAtAction(nameof(GetById), new { id = r.Id }, r);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, [FromBody] ProcessReturnDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var r = await _returns.ProcessAsync(id, dto, Guid.Parse(userId));
        return r == null ? NotFound() : Ok(r);
    }
}
