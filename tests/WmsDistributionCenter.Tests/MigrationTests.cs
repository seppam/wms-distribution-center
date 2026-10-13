using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using WmsDistributionCenter.Api.Data;

namespace WmsDistributionCenter.Tests;

public class MigrationTests
{
    private static WmsDbContext RelationalContext() =>
        new(new DbContextOptionsBuilder<WmsDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

    [Fact]
    public void ModelSnapshot_MatchesTheCurrentModel_NoPendingMigration()
    {
        using var db = RelationalContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var snapshotModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot!.Model);
        var differ = db.GetService<IMigrationsModelDiffer>();
        var changes = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(changes.Count == 0,
            "The model has changes that are not in a migration. Run: dotnet ef migrations add <Name> --project backend --output-dir Data/Migrations. Pending: "
            + string.Join(", ", changes.Select(c => c.GetType().Name)));
    }

    [Fact]
    public void Migrations_Exist_AndStartWithInitialCreate()
    {
        using var db = RelationalContext();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order().ToList();
        Assert.NotEmpty(migrations);
        Assert.EndsWith("InitialCreate", migrations[0]);
    }
}
