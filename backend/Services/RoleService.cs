using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.DTOs.Role;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class RoleService : IRoleService
{
    private readonly WmsDbContext _db;

    public RoleService(WmsDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<RoleResponseDto>> GetAllAsync()
    {
        var roles = await _db.Roles.ToListAsync();
        return roles.Select(x => x.ToDto()).ToList();
    }

    public async Task<RoleResponseDto?> GetByIdAsync(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        return role?.ToDto();
    }

    public async Task<RoleResponseDto> CreateAsync(CreateRoleDto dto)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Permissions = string.IsNullOrEmpty(dto.Permissions) ? null : JsonDocument.Parse(dto.Permissions),
            IsSystem = dto.IsSystem,
            CreatedAt = DateTime.UtcNow
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role.ToDto();
    }

    public async Task<RoleResponseDto?> UpdateAsync(Guid id, UpdateRoleDto dto)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return null;

        if (dto.Name != null) role.Name = dto.Name;
        if (dto.Description != null) role.Description = dto.Description;
        if (dto.Permissions != null) role.Permissions = JsonDocument.Parse(dto.Permissions);
        if (dto.IsSystem.HasValue) role.IsSystem = dto.IsSystem.Value;

        await _db.SaveChangesAsync();
        return role.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null || role.IsSystem) return false;
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return true;
    }
}
