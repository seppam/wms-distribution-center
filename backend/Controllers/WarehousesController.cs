using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Warehouse;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly WmsDbContext _db;

    public WarehousesController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var warehouses = await _db.Warehouses.ToListAsync();
        return Ok(warehouses.Select(w => new WarehouseResponseDto(w.Id, w.Code, w.Name, w.Address, w.City, w.Capacity, w.IsActive, w.CreatedAt)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var w = await _db.Warehouses.FindAsync(id);
        return w == null ? NotFound() : Ok(new WarehouseResponseDto(w.Id, w.Code, w.Name, w.Address, w.City, w.Capacity, w.IsActive, w.CreatedAt));
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseDto dto)
    {
        var w = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            Address = dto.Address,
            City = dto.City,
            Capacity = dto.Capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Warehouses.Add(w);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = w.Id }, new WarehouseResponseDto(w.Id, w.Code, w.Name, w.Address, w.City, w.Capacity, w.IsActive, w.CreatedAt));
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseDto dto)
    {
        var w = await _db.Warehouses.FindAsync(id);
        if (w == null) return NotFound();
        if (dto.Name != null) w.Name = dto.Name;
        if (dto.Address != null) w.Address = dto.Address;
        if (dto.City != null) w.City = dto.City;
        if (dto.Capacity.HasValue) w.Capacity = dto.Capacity.Value;
        if (dto.IsActive.HasValue) w.IsActive = dto.IsActive.Value;
        await _db.SaveChangesAsync();
        return Ok(new WarehouseResponseDto(w.Id, w.Code, w.Name, w.Address, w.City, w.Capacity, w.IsActive, w.CreatedAt));
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var w = await _db.Warehouses.FindAsync(id);
        if (w == null) return NotFound();
        _db.Warehouses.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
