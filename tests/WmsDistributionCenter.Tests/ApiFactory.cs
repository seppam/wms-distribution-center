using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WmsDistributionCenter.Api.Data;

namespace WmsDistributionCenter.Tests;

/// <summary>
/// Hosts the real API pipeline (authentication, authorization policies, rate limiting, exception handling)
/// on an in-memory database seeded with the demo accounts and demo warehouse data.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Test!Pass12345";
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _root = new();
    private readonly int _authPermits;
    private readonly bool _allowRegistration;

    public ApiFactory(int authPermits = 10_000, bool allowRegistration = true)
    {
        _authPermits = authPermits;
        _allowRegistration = allowRegistration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-A1b2C3d4E5f6G7h8");
        builder.UseSetting("Jwt:Issuer", "test");
        builder.UseSetting("Jwt:Audience", "test");
        builder.UseSetting("Auth:AllowSelfRegistration", _allowRegistration ? "true" : "false");
        builder.UseSetting("RateLimiting:AuthPermitLimit", _authPermits.ToString());
        builder.UseSetting("AllowedOrigins:0", "http://localhost:5173");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WmsDbContext>>();
            services.RemoveAll<WmsDbContext>();
            var options = new DbContextOptionsBuilder<WmsDbContext>()
                .UseInMemoryDatabase(_dbName, _root).Options;
            services.AddScoped<WmsDbContext>(_ => new TestDbContext(options));
        });
    }

    /// <summary>Seeds roles, the four demo users and demo warehouse data (Development-style seeding).</summary>
    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db, new SeedOptions(DemoData: true, IsDevelopment: true, DemoPassword: DemoPassword));
    }

    public static async Task<ApiFactory> CreateSeededAsync(int authPermits = 10_000, bool allowRegistration = true)
    {
        var f = new ApiFactory(authPermits, allowRegistration);
        await f.SeedAsync();
        return f;
    }

    /// <summary>Creates a client authenticated as one of the demo users (admin, supervisor, operator, viewer).</summary>
    public async Task<HttpClient> ClientAsAsync(string? username)
    {
        var client = CreateClient();
        if (username is null) return client;
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username, password = DemoPassword });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private sealed record LoginBody(string Token);
}
