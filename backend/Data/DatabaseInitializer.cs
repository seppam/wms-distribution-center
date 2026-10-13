using Microsoft.EntityFrameworkCore;

namespace WmsDistributionCenter.Api.Data;

/// <summary>
/// Brings the database schema up to date and seeds reference data when the host starts.
/// Runs as a hosted service so EF design-time tooling (which builds the host without starting it) never touches the database.
/// </summary>
public sealed class DatabaseInitializer(IServiceProvider services, IConfiguration config, IHostEnvironment env, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        if (db.Database.IsRelational())
        {
            var migrate = env.IsDevelopment() || config.GetValue<bool>("Database:MigrateOnStartup");
            if (migrate)
            {
                logger.LogInformation("Applying pending EF Core migrations");
                await db.Database.MigrateAsync(ct);
            }
            else if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            {
                throw new InvalidOperationException(
                    "The database schema is out of date. Apply migrations with 'dotnet ef database update --project backend' " +
                    "(or run the generated SQL script), or set Database__MigrateOnStartup=true.");
            }
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct); // in-memory provider used by tests
        }

        await DbSeeder.SeedAsync(db, new SeedOptions(
            DemoData: config.GetValue<bool>("Seed:DemoData"),
            IsDevelopment: env.IsDevelopment(),
            DemoPassword: config["Seed:DemoPassword"],
            AdminUsername: config["Seed:AdminUsername"],
            AdminEmail: config["Seed:AdminEmail"],
            AdminPassword: config["Seed:AdminPassword"]), logger);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
