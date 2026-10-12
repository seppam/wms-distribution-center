using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Carrier;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CarriersController : ControllerBase
{
    private readonly WmsDbContext _db;

    public CarriersController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var carriers = await _db.Carriers.ToListAsync();
        return Ok(carriers.Select(c => new CarrierResponseDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.VehicleType, c.IsActive)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.Carriers.FindAsync(id);
        return c == null ? NotFound() : Ok(new CarrierResponseDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.VehicleType, c.IsActive));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCarrierDto dto)
    {
        var c = new Carrier
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ContactPerson = dto.ContactPerson,
            Phone = dto.Phone,
            Email = dto.Email,
            VehicleType = dto.VehicleType,
            IsActive = true
        };
        _db.Carriers.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, new CarrierResponseDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.VehicleType, c.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierDto dto)
    {
        var c = await _db.Carriers.FindAsync(id);
        if (c == null) return NotFound();
        if (dto.Name != null) c.Name = dto.Name;
        if (dto.ContactPerson != null) c.ContactPerson = dto.ContactPerson;
        if (dto.Phone != null) c.Phone = dto.Phone;
        if (dto.Email != null) c.Email = dto.Email;
        if (dto.VehicleType != null) c.VehicleType = dto.VehicleType;
        if (dto.IsActive.HasValue) c.IsActive = dto.IsActive.Value;
        await _db.SaveChangesAsync();
        return Ok(new CarrierResponseDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.VehicleType, c.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var c = await _db.Carriers.FindAsync(id);
        if (c == null) return NotFound();
        _db.Carriers.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
