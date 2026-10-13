using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutboundOrdersController : ControllerBase
{
    private readonly IOutboundService _outboundService;

    public OutboundOrdersController(IOutboundService outboundService)
    {
        _outboundService = outboundService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _outboundService.GetAllAsync();
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _outboundService.GetByIdAsync(id);
        return order == null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOutboundOrderDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var order = await _outboundService.CreateAsync(dto, Guid.Parse(userId));
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOutboundOrderDto dto)
    {
        var order = await _outboundService.UpdateAsync(id, dto);
        return order == null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _outboundService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/items")]
    public async Task<IActionResult> GetItems(Guid id)
    {
        var items = await _outboundService.GetOrderItemsAsync(id);
        return Ok(items);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddOrderItemDto dto)
    {
        var item = await _outboundService.AddItemAsync(id, dto);
        return Ok(item);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("{id:guid}/start-picking")]
    public async Task<IActionResult> StartPicking(Guid id)
    {
        var order = await _outboundService.StartPickingAsync(id);
        return order == null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("{id:guid}/assign-picker")]
    public async Task<IActionResult> AssignPicker(Guid id, [FromBody] AssignPickerDto dto)
    {
        var order = await _outboundService.AssignPickerAsync(id, dto);
        return order == null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/pack")]
    public async Task<IActionResult> Pack(Guid id, [FromBody] PackOrderDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var order = await _outboundService.PackOrderAsync(id, dto, Guid.Parse(userId));
        return order == null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, [FromBody] CreateShipmentDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        try
        {
            var shipment = await _outboundService.ShipOrderAsync(id, dto, Guid.Parse(userId));
            return Ok(shipment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
