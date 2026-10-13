using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WmsDistributionCenter.Api.Security;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Location;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly WmsDbContext _db;

    public LocationsController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? zoneId, [FromQuery] string? code)
    {
        var query = _db.Locations
            .Include(l => l.Zone).ThenInclude(z => z.Warehouse)
            .AsQueryable();

        if (zoneId.HasValue)
            query = query.Where(l => l.ZoneId == zoneId.Value);

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(l => l.LocationCode.Contains(code));

        var locations = await query.ToListAsync();
        return Ok(locations.Select(l => new LocationResponseDto(
            l.Id, l.ZoneId, l.Zone.Name, l.Zone.Warehouse.Name,
            l.LocationCode, l.Aisle, l.Rack, l.Level, l.Bin,
            l.LocationType, l.MaxCapacity, l.Status)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var l = await _db.Locations
            .Include(loc => loc.Zone).ThenInclude(z => z.Warehouse)
            .FirstOrDefaultAsync(loc => loc.Id == id);
        return l == null ? NotFound() : Ok(new LocationResponseDto(
            l.Id, l.ZoneId, l.Zone.Name, l.Zone.Warehouse.Name,
            l.LocationCode, l.Aisle, l.Rack, l.Level, l.Bin,
            l.LocationType, l.MaxCapacity, l.Status));
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto dto)
    {
        var l = new Location
        {
            Id = Guid.NewGuid(),
            ZoneId = dto.ZoneId,
            LocationCode = dto.LocationCode,
            Aisle = dto.Aisle,
            Rack = dto.Rack,
            Level = dto.Level,
            Bin = dto.Bin,
            LocationType = dto.LocationType,
            MaxCapacity = dto.MaxCapacity,
            Status = WmsDistributionCenter.Api.Enums.LocationStatus.Empty
        };
        _db.Locations.Add(l);
        await _db.SaveChangesAsync();
        await _db.Entry(l).Reference(x => x.Zone).LoadAsync();
        await _db.Entry(l.Zone).Reference(z => z.Warehouse).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = l.Id }, new LocationResponseDto(
            l.Id, l.ZoneId, l.Zone.Name, l.Zone.Warehouse.Name,
            l.LocationCode, l.Aisle, l.Rack, l.Level, l.Bin,
            l.LocationType, l.MaxCapacity, l.Status));
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationDto dto)
    {
        var l = await _db.Locations.FindAsync(id);
        if (l == null) return NotFound();
        if (dto.LocationCode != null) l.LocationCode = dto.LocationCode;
        if (dto.Aisle != null) l.Aisle = dto.Aisle;
        if (dto.Rack != null) l.Rack = dto.Rack;
        if (dto.Level != null) l.Level = dto.Level;
        if (dto.Bin != null) l.Bin = dto.Bin;
        if (dto.LocationType.HasValue) l.LocationType = dto.LocationType.Value;
        if (dto.MaxCapacity.HasValue) l.MaxCapacity = dto.MaxCapacity.Value;
        if (dto.Status.HasValue) l.Status = dto.Status.Value;
        await _db.SaveChangesAsync();
        await _db.Entry(l).Reference(x => x.Zone).LoadAsync();
        await _db.Entry(l.Zone).Reference(z => z.Warehouse).LoadAsync();
        return Ok(new LocationResponseDto(
            l.Id, l.ZoneId, l.Zone.Name, l.Zone.Warehouse.Name,
            l.LocationCode, l.Aisle, l.Rack, l.Level, l.Bin,
            l.LocationType, l.MaxCapacity, l.Status));
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var l = await _db.Locations.FindAsync(id);
        if (l == null) return NotFound();
        _db.Locations.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
