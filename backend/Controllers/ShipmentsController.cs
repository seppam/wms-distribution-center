using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShipmentsController : ControllerBase
{
    private readonly IOutboundService _outbound;

    public ShipmentsController(IOutboundService outbound) => _outbound = outbound;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _outbound.GetShipmentsAsync());

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id)
    {
        var shipment = await _outbound.MarkDeliveredAsync(id);
        return shipment == null ? NotFound() : Ok(shipment);
    }
}
