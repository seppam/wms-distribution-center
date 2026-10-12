using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.DTOs.Auth;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class AuthService : IAuthService
{
    private readonly WmsDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(WmsDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Warehouse)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480"));

        return new LoginResponse(
            token,
            expiry,
            new UserInfo(
                user.Id,
                user.Username,
                user.Email,
                user.FullName,
                user.Role.Name,
                user.Warehouse?.Name,
                user.Phone
            )
        );
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email))
            return null;

        // Public self-registration always gets the least-privileged role. Admins assign roles via /api/users.
        var roleId = (await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer"))?.Id
            ?? throw new InvalidOperationException("Default role not found");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            RoleId = roleId,
            WarehouseId = request.WarehouseId,
            Phone = request.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _db.Entry(user).Reference(u => u.Role).LoadAsync();
        await _db.Entry(user).Reference(u => u.Warehouse).LoadAsync();

        var token = GenerateToken(user);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480"));

        return new LoginResponse(
            token,
            expiry,
            new UserInfo(
                user.Id,
                user.Username,
                user.Email,
                user.FullName,
                user.Role.Name,
                user.Warehouse?.Name,
                user.Phone
            )
        );
    }

    public async Task<UserInfo?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Warehouse)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        return new UserInfo(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role.Name,
            user.Warehouse?.Name,
            user.Phone
        );
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.Name)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
