using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.User;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class UserService : IUserService
{
    private readonly WmsDbContext _db;
    private readonly IMapper _mapper;

    public UserService(WmsDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllAsync()
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Warehouse)
            .ToListAsync();
        return _mapper.Map<IEnumerable<UserResponseDto>>(users);
    }

    public async Task<UserResponseDto?> GetByIdAsync(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Warehouse)
            .FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? null : _mapper.Map<UserResponseDto>(user);
    }

    public async Task<UserResponseDto> CreateAsync(CreateUserDto dto)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName,
            RoleId = dto.RoleId,
            WarehouseId = dto.WarehouseId,
            Phone = dto.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _db.Entry(user).Reference(u => u.Role).LoadAsync();
        await _db.Entry(user).Reference(u => u.Warehouse).LoadAsync();

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task<UserResponseDto?> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        if (dto.Email != null) user.Email = dto.Email;
        if (dto.FullName != null) user.FullName = dto.FullName;
        if (dto.Password != null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        if (dto.RoleId.HasValue) user.RoleId = dto.RoleId.Value;
        if (dto.WarehouseId.HasValue) user.WarehouseId = dto.WarehouseId;
        if (dto.Phone != null) user.Phone = dto.Phone;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _db.Entry(user).Reference(u => u.Role).LoadAsync();
        await _db.Entry(user).Reference(u => u.Warehouse).LoadAsync();

        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }
}
