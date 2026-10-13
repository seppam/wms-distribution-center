using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] InventoryQueryDto query)
    {
        var result = await _inventoryService.QueryAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _inventoryService.GetByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpGet("adjustments")]
    public async Task<IActionResult> GetAdjustments()
    {
        var adj = await _inventoryService.GetAdjustmentsAsync();
        return Ok(adj);
    }

    [Authorize(Policy = Policies.Operate)]
    [HttpPost("adjustments")]
    public async Task<IActionResult> CreateAdjustment([FromBody] CreateAdjustmentDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var adj = await _inventoryService.CreateAdjustmentAsync(dto, Guid.Parse(userId));
        return Ok(adj);
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost("adjustments/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAdjustment(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var adj = await _inventoryService.ApproveAdjustmentAsync(id, Guid.Parse(userId));
        return adj == null ? NotFound() : Ok(adj);
    }
}
