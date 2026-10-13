using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Uom;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Security;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UomsController : ControllerBase
{
    private readonly WmsDbContext _db;

    public UomsController(WmsDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var uoms = await _db.Uoms.Include(u => u.BaseUom).OrderBy(u => u.Code).ToListAsync();
        return Ok(uoms.Select(u => u.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var u = await _db.Uoms.Include(x => x.BaseUom).FirstOrDefaultAsync(x => x.Id == id);
        return u == null ? NotFound() : Ok(u.ToDto());
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUomDto dto)
    {
        if (dto.ConversionFactor <= 0) throw new BusinessRuleException("The conversion factor must be greater than zero.");
        if (await _db.Uoms.AnyAsync(u => u.Code == dto.Code)) throw new ConflictException("A unit with this code already exists.");
        var u = new Uom { Id = Guid.NewGuid(), Code = dto.Code.Trim().ToUpperInvariant(), Name = dto.Name, BaseUomId = dto.BaseUomId, ConversionFactor = dto.ConversionFactor, IsBase = dto.IsBase };
        _db.Uoms.Add(u);
        await _db.SaveChangesAsync();
        await _db.Entry(u).Reference(x => x.BaseUom).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = u.Id }, u.ToDto());
    }

    [Authorize(Policy = Policies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUomDto dto)
    {
        var u = await _db.Uoms.FindAsync(id);
        if (u == null) return NotFound();
        if (dto.ConversionFactor is <= 0) throw new BusinessRuleException("The conversion factor must be greater than zero.");
        if (dto.BaseUomId == id) throw new BusinessRuleException("A unit cannot be its own base unit.");
        if (dto.Name != null) u.Name = dto.Name;
        if (dto.BaseUomId.HasValue) u.BaseUomId = dto.BaseUomId;
        if (dto.ConversionFactor.HasValue) u.ConversionFactor = dto.ConversionFactor.Value;
        if (dto.IsBase.HasValue) u.IsBase = dto.IsBase.Value;
        await _db.SaveChangesAsync();
        await _db.Entry(u).Reference(x => x.BaseUom).LoadAsync();
        return Ok(u.ToDto());
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var u = await _db.Uoms.FindAsync(id);
        if (u == null) return NotFound();
        _db.Uoms.Remove(u);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
