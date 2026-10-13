using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;
 using WmsDistributionCenter.Api.DTOs.Transfer;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transfers;

    public TransfersController(ITransferService transfers) => _transfers = transfers;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _transfers.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var t = await _transfers.GetByIdAsync(id);
        return t == null ? NotFound() : Ok(t);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransferDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var t = await _transfers.CreateAsync(dto, Guid.Parse(userId));
        return CreatedAtAction(nameof(GetById), new { id = t.Id }, t);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var t = await _transfers.CompleteAsync(id, Guid.Parse(userId));
        return t == null ? NotFound() : Ok(t);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var t = await _transfers.CancelAsync(id);
        return t == null ? NotFound() : Ok(t);
    }
}
