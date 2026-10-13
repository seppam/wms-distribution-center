using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;
 using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/pick-tasks")]
[Authorize]
public class PickTasksController : ControllerBase
{
    private readonly IOutboundService _outbound;

    public PickTasksController(IOutboundService outbound) => _outbound = outbound;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PickTaskStatus? status, [FromQuery] Guid? orderId, [FromQuery] bool mine = false)
    {
        Guid? pickerId = null;
        if (mine && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var me)) pickerId = me;
        return Ok(await _outbound.GetPickTasksAsync(status, orderId, pickerId));
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmPickDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var task = await _outbound.ConfirmPickAsync(id, dto, Guid.Parse(userId));
        return task == null ? NotFound() : Ok(task);
    }
}
