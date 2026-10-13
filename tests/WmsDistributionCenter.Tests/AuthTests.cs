using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.DTOs.Auth;
using WmsDistributionCenter.Api.Models;
using WmsDistributionCenter.Api.Services;

namespace WmsDistributionCenter.Tests;

public class AuthTests
{
    private static JwtSettings Config() => new("unit-test-key-unit-test-key-unit-test-key-0123456789", "test", "test", 5);

    [Fact]
    public async Task Register_CannotSelfAssignAdminRole()
    {
        using var f = new TestFixture();
        var viewer = new Role { Id = Guid.NewGuid(), Name = "Viewer", IsSystem = true };
        f.Db.Roles.Add(viewer);
        await f.Db.SaveChangesAsync();
        var admin = await f.Db.Roles.FirstAsync(r => r.Name == "Admin");
        var svc = new AuthService(f.Db, Config());

        var result = await svc.RegisterAsync(new RegisterRequest("newuser", "new@example.com", "Passw0rd!", "New User", admin.Id, null, null));

        Assert.NotNull(result);
        Assert.Equal("Viewer", result!.User.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        using var f = new TestFixture();
        var svc = new AuthService(f.Db, Config());
        Assert.Null(await svc.LoginAsync(new LoginRequest("tester", "wrong")));
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        using var f = new TestFixture();
        var svc = new AuthService(f.Db, Config());
        var res = await svc.LoginAsync(new LoginRequest("tester", "secret-demo"));
        Assert.False(string.IsNullOrEmpty(res?.Token));
    }
}
