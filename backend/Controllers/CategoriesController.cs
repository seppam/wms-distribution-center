using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Category;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly WmsDbContext _db;

    public CategoriesController(WmsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories.ToListAsync();
        return Ok(categories.Select(c => new CategoryResponseDto(c.Id, c.Name, c.ParentId, null, c.Level, c.Description)));
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var all = await _db.Categories.ToListAsync();
        var roots = all.Where(c => c.ParentId == null).ToList();

        CategoryTreeDto buildTree(Category cat) => new(
            cat.Id, cat.Name, cat.Level, cat.Description,
            all.Where(c => c.ParentId == cat.Id).Select(buildTree).ToList()
        );

        return Ok(roots.Select(buildTree));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        return Ok(new CategoryResponseDto(cat.Id, cat.Name, cat.ParentId, null, cat.Level, cat.Description));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
    {
        var cat = new WmsDistributionCenter.Api.Models.Category
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ParentId = dto.ParentId,
            Level = dto.ParentId == null ? 1 : (_db.Categories.Find(dto.ParentId)?.Level ?? 1) + 1,
            Description = dto.Description
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cat.Id },
            new CategoryResponseDto(cat.Id, cat.Name, cat.ParentId, null, cat.Level, cat.Description));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryDto dto)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        if (dto.Name != null) cat.Name = dto.Name;
        if (dto.ParentId.HasValue) cat.ParentId = dto.ParentId;
        if (dto.Description != null) cat.Description = dto.Description;
        await _db.SaveChangesAsync();
        return Ok(new CategoryResponseDto(cat.Id, cat.Name, cat.ParentId, null, cat.Level, cat.Description));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
