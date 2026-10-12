using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Supplier;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly WmsDbContext _db;

    public SuppliersController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var suppliers = await _db.Suppliers.ToListAsync();
        return Ok(suppliers.Select(s => new SupplierResponseDto(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.IsActive)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        return s == null ? NotFound() : Ok(new SupplierResponseDto(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.IsActive));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierDto dto)
    {
        var s = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            ContactPerson = dto.ContactPerson,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            IsActive = true
        };
        _db.Suppliers.Add(s);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, new SupplierResponseDto(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierDto dto)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s == null) return NotFound();
        if (dto.Name != null) s.Name = dto.Name;
        if (dto.ContactPerson != null) s.ContactPerson = dto.ContactPerson;
        if (dto.Email != null) s.Email = dto.Email;
        if (dto.Phone != null) s.Phone = dto.Phone;
        if (dto.Address != null) s.Address = dto.Address;
        if (dto.IsActive.HasValue) s.IsActive = dto.IsActive.Value;
        await _db.SaveChangesAsync();
        return Ok(new SupplierResponseDto(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s == null) return NotFound();
        _db.Suppliers.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
