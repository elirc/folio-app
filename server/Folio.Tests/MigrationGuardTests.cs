using Folio.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Folio.Tests;

/// <summary>
/// Guards against the schema drifting away from the migrations. The integration
/// harness applies migrations to an in-memory SQLite database, but a model change
/// that is never captured in a migration would still "work" there while breaking
/// a freshly-migrated production database — exactly the missing-migration gap that
/// has shipped in a sibling repo.
/// </summary>
public class MigrationGuardTests
{
    private static DbContextOptions<FolioDbContext> Options(SqliteConnection connection) =>
        new DbContextOptionsBuilder<FolioDbContext>()
            .UseSqlite(connection)
            // Mirror the production registration so model-building emits the same
            // (expected) diagnostics rather than throwing on the required
            // Block→Page navigation under the soft-delete query filter.
            .ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;

    [Fact]
    public void Model_matches_the_migrations_snapshot_no_pending_changes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new FolioDbContext(Options(connection));

        var differ = db.GetService<IMigrationsModelDiffer>();
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var runtimeInitializer = db.GetService<IModelRuntimeInitializer>();

        var snapshot = migrationsAssembly.ModelSnapshot;
        Assert.NotNull(snapshot); // there must be a model snapshot at all

        // The snapshot model must be finalized + runtime-initialized before it has
        // a relational model to diff against the live model.
        var snapshotModel = snapshot!.Model;
        if (snapshotModel is IMutableModel mutable)
        {
            snapshotModel = mutable.FinalizeModel();
        }
        snapshotModel = runtimeInitializer.Initialize(snapshotModel);

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        // A non-empty diff means the entity model changed without a matching
        // migration — add one with `dotnet ef migrations add`.
        Assert.Empty(differences);
    }

    [Fact]
    public void Seeder_runs_against_a_migrated_database()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new FolioDbContext(Options(connection));

        // The real application path: apply migrations (not EnsureCreated), then seed.
        db.Database.Migrate();
        DbSeeder.Seed(db);

        // Same shape asserted by PersistenceTests, but against a Migrate()'d schema:
        // two workspaces (Acme + Globex), four members, eight pages, some blocks.
        Assert.Equal(2, db.Workspaces.Count());
        Assert.Equal(4, db.Members.Count());
        Assert.Equal(8, db.Pages.Count());
        Assert.True(db.Blocks.Any());

        // Seeding is idempotent: a second pass is a no-op, not a duplicate.
        DbSeeder.Seed(db);
        Assert.Equal(2, db.Workspaces.Count());
        Assert.Equal(8, db.Pages.Count());
    }
}
