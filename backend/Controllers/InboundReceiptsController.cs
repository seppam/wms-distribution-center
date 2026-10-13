using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InboundReceiptsController : ControllerBase
{
    private readonly IInboundService _inboundService;

    public InboundReceiptsController(IInboundService inboundService)
    {
        _inboundService = inboundService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var receipts = await _inboundService.GetAllAsync();
        return Ok(receipts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var receipt = await _inboundService.GetByIdAsync(id);
        return receipt == null ? NotFound() : Ok(receipt);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInboundReceiptDto dto)
    {
        var receipt = await _inboundService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInboundReceiptDto dto)
    {
        var receipt = await _inboundService.UpdateAsync(id, dto);
        return receipt == null ? NotFound() : Ok(receipt);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _inboundService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddReceiptItemDto dto)
    {
        var item = await _inboundService.AddItemAsync(id, dto);
        return Ok(item);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> ReceiveItem(Guid id, [FromBody] ReceiveItemDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var item = await _inboundService.ReceiveItemAsync(id, dto, Guid.Parse(userId));
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var receipt = await _inboundService.CompleteReceiptAsync(id, Guid.Parse(userId));
        return receipt == null ? NotFound() : Ok(receipt);
    }
}
