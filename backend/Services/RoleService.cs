using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Infrastructure;
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
            IsSystem = false, // only the seeded roles are built-in
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

        // Authorization policies reference the built-in role names, so those can never be renamed or demoted.
        if (role.IsSystem && ((dto.Name != null && dto.Name != role.Name) || dto.IsSystem == false))
            throw new BusinessRuleException("Built-in roles cannot be renamed or turned into custom roles.");

        if (dto.Name != null) role.Name = dto.Name;
        if (dto.Description != null) role.Description = dto.Description;
        if (dto.Permissions != null) role.Permissions = JsonDocument.Parse(dto.Permissions);
        if (dto.IsSystem == true && !role.IsSystem)
            throw new BusinessRuleException("Custom roles cannot be promoted to built-in roles.");

        await _db.SaveChangesAsync();
        return role.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return false;
        if (role.IsSystem) throw new BusinessRuleException("Built-in roles cannot be deleted.");
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return true;
    }
}
