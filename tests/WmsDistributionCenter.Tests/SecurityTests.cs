using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Security;

namespace WmsDistributionCenter.Tests;

public class JwtSettingsTests
{
    private static IConfiguration Cfg(string? key, string issuer = "i", string audience = "a", string expiry = "60") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = key, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience, ["Jwt:ExpiryMinutes"] = expiry
        }).Build();

    private static IHostEnvironment Env(string name) => new HostingEnvironment { EnvironmentName = name };

    private const string GoodKey = "q7Zr2xLm9TpV4bNc8WdK1yHs6GfJ3aEu";

    [Fact]
    public void Production_RequiresAKey() =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(Cfg(null), Env("Production")));

    [Theory]
    [InlineData("short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("please-change-me-to-a-real-secret-value-123456")]
    public void Production_RejectsWeakOrPlaceholderKeys(string key) =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(Cfg(key), Env("Production")));

    [Fact]
    public void Production_AcceptsAStrongKey() =>
        Assert.Equal(GoodKey, JwtSettings.Load(Cfg(GoodKey), Env("Production")).Key);

    [Fact]
    public void Development_GeneratesAnEphemeralKey_WhenMissing()
    {
        var a = JwtSettings.Load(Cfg(null), Env("Development"));
        var b = JwtSettings.Load(Cfg(null), Env("Development"));
        Assert.True(a.Key.Length >= 32);
        Assert.NotEqual(a.Key, b.Key);
    }

    [Theory]
    [InlineData("", "a", "60")]
    [InlineData("i", "", "60")]
    [InlineData("i", "a", "0")]
    [InlineData("i", "a", "99999")]
    [InlineData("i", "a", "abc")]
    public void RejectsInvalidIssuerAudienceOrExpiry(string issuer, string audience, string expiry) =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(Cfg(GoodKey, issuer, audience, expiry), Env("Production")));
}

public class SeedingTests
{
    [Fact]
    public async Task OutsideDevelopment_NoDemoAccountsAreCreated_EvenWithTheDemoFlag()
    {
        using var f = new TestFixture();
        f.Db.Users.RemoveRange(f.Db.Users); f.Db.Roles.RemoveRange(f.Db.Roles);
        await f.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(f.Db, new SeedOptions(DemoData: true, IsDevelopment: false));

        Assert.Empty(f.Db.Users);
        Assert.Equal(4, await f.Db.Roles.CountAsync());
    }

    [Fact]
    public async Task InDevelopment_DemoFlagCreatesOneAccountPerRole()
    {
        using var f = new TestFixture();
        f.Db.Users.RemoveRange(f.Db.Users); f.Db.Roles.RemoveRange(f.Db.Roles);
        await f.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(f.Db, new SeedOptions(DemoData: true, IsDevelopment: true));

        var users = await f.Db.Users.Include(u => u.Role).ToListAsync();
        Assert.Equal(new[] { "Admin", "Operator", "Supervisor", "Viewer" }, users.Select(u => u.Role.Name).Order());
    }

    [Fact]
    public async Task BootstrapAdmin_RequiresALongPassword_AndIsCreatedFromConfigOnly()
    {
        using var f = new TestFixture();
        f.Db.Users.RemoveRange(f.Db.Users); f.Db.Roles.RemoveRange(f.Db.Roles);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbSeeder.SeedAsync(f.Db, new SeedOptions(AdminPassword: "short")));

        await DbSeeder.SeedAsync(f.Db, new SeedOptions(AdminUsername: "root", AdminPassword: "a-long-bootstrap-pass"));
        var user = await f.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("root", user.Username);
        Assert.Equal("Admin", user.Role.Name);
    }
}

public class ApiSecurityTests
{
    [Fact]
    public async Task Cors_AllowsConfiguredOrigin_AndOmitsHeadersForOthers()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var client = f.CreateClient();

        async Task<IEnumerable<string>?> AcaoFor(string origin)
        {
            using var req = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
            req.Headers.Add("Origin", origin);
            req.Headers.Add("Access-Control-Request-Method", "POST");
            var res = await client.SendAsync(req);
            return res.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v : null;
        }

        Assert.Equal("http://localhost:5173", Assert.Single((await AcaoFor("http://localhost:5173"))!));
        Assert.Null(await AcaoFor("https://evil.example"));
    }

    [Fact]
    public async Task AuthEndpoints_AreRateLimited()
    {
        using var f = await ApiFactory.CreateSeededAsync(authPermits: 3);
        var client = f.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "wrong-password" })).StatusCode);

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(2, codes.Count(c => c == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Register_IsDisabledByDefaultConfiguration()
    {
        using var f = await ApiFactory.CreateSeededAsync(allowRegistration: false);
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { username = "newbie", email = "n@example.com", password = "Sup3r-long-pass", fullName = "New Bie" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEnabled_AlwaysCreatesAViewer()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { username = "newbie", email = "n@example.com", password = "Sup3r-long-pass", fullName = "New Bie" });
        res.EnsureSuccessStatusCode();
        var client = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "newbie", password = "Sup3r-long-pass" });
        Assert.Contains("\"role\":\"Viewer\"", await client.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WeakPasswords_AreRejected()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { username = "newbie", email = "n@example.com", password = "short", fullName = "New Bie" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Token_OfADeactivatedUser_IsRejectedImmediately()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var client = await f.ClientAsAsync("operator");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
            (await db.Users.SingleAsync(u => u.Username == "operator")).IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Token_IsRejected_WhenTheUsersRoleChangedAfterIssue()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var client = await f.ClientAsAsync("supervisor");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
            var viewer = await db.Roles.SingleAsync(r => r.Name == "Viewer");
            (await db.Users.SingleAsync(u => u.Username == "supervisor")).RoleId = viewer.Id;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeleteOwnAccount()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var client = await f.ClientAsAsync("admin");
        var me = await client.GetFromJsonAsync<Dictionary<string, object>>("/api/auth/me");
        var res = await client.DeleteAsync($"/api/users/{me!["id"]}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var res = await f.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }
}

public class RoleProtectionTests
{
    [Fact]
    public async Task BuiltInRoles_CannotBeRenamedOrDeleted_AndNewRolesAreNeverBuiltIn()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var admin = await f.ClientAsAsync("admin");
        var roles = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/roles");
        var adminRole = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Admin").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/roles/{adminRole}", new { name = "Root" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/roles/{adminRole}")).StatusCode);

        var created = await admin.PostAsJsonAsync("/api/roles", new { name = "Auditor", isSystem = true });
        created.EnsureSuccessStatusCode();
        Assert.Contains("\"isSystem\":false", await created.Content.ReadAsStringAsync());
    }
}
