using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Role;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class RoleService : IRoleService
{
    private readonly WmsDbContext _db;
    private readonly IMapper _mapper;

    public RoleService(WmsDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IEnumerable<RoleResponseDto>> GetAllAsync()
    {
        var roles = await _db.Roles.ToListAsync();
        return _mapper.Map<IEnumerable<RoleResponseDto>>(roles);
    }

    public async Task<RoleResponseDto?> GetByIdAsync(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        return role == null ? null : _mapper.Map<RoleResponseDto>(role);
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
        return _mapper.Map<RoleResponseDto>(role);
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
        return _mapper.Map<RoleResponseDto>(role);
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
