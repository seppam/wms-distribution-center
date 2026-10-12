using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Zone;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ZonesController : ControllerBase
{
    private readonly WmsDbContext _db;

    public ZonesController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? warehouseId)
    {
        var query = _db.Zones.Include(z => z.Warehouse).AsQueryable();
        if (warehouseId.HasValue)
            query = query.Where(z => z.WarehouseId == warehouseId.Value);

        var zones = await query.ToListAsync();
        return Ok(zones.Select(z => new ZoneResponseDto(z.Id, z.WarehouseId, z.Warehouse.Name, z.Name, z.ZoneType, z.Description, z.IsActive)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var z = await _db.Zones.Include(z => z.Warehouse).FirstOrDefaultAsync(z => z.Id == id);
        return z == null ? NotFound() : Ok(new ZoneResponseDto(z.Id, z.WarehouseId, z.Warehouse.Name, z.Name, z.ZoneType, z.Description, z.IsActive));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateZoneDto dto)
    {
        var z = new Zone
        {
            Id = Guid.NewGuid(),
            WarehouseId = dto.WarehouseId,
            Name = dto.Name,
            ZoneType = dto.ZoneType,
            Description = dto.Description,
            IsActive = true
        };
        _db.Zones.Add(z);
        await _db.SaveChangesAsync();
        await _db.Entry(z).Reference(x => x.Warehouse).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = z.Id }, new ZoneResponseDto(z.Id, z.WarehouseId, z.Warehouse.Name, z.Name, z.ZoneType, z.Description, z.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateZoneDto dto)
    {
        var z = await _db.Zones.FindAsync(id);
        if (z == null) return NotFound();
        if (dto.Name != null) z.Name = dto.Name;
        if (dto.ZoneType.HasValue) z.ZoneType = dto.ZoneType.Value;
        if (dto.Description != null) z.Description = dto.Description;
        if (dto.IsActive.HasValue) z.IsActive = dto.IsActive.Value;
        await _db.SaveChangesAsync();
        await _db.Entry(z).Reference(x => x.Warehouse).LoadAsync();
        return Ok(new ZoneResponseDto(z.Id, z.WarehouseId, z.Warehouse.Name, z.Name, z.ZoneType, z.Description, z.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var z = await _db.Zones.FindAsync(id);
        if (z == null) return NotFound();
        _db.Zones.Remove(z);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
