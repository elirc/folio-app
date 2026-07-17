using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Folio.Tests;

/// <summary>
/// Boots the real API against a private in-memory SQLite database. The single
/// connection is kept open for the factory's lifetime so the schema and seeded
/// data persist across scoped DbContext instances. No external server or file
/// database is touched.
/// </summary>
public class FolioApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // Development so the startup path also seeds sample data.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<FolioDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<FolioDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Runs an action against a fresh scoped <see cref="FolioDbContext"/>.</summary>
    public async Task<T> WithDbAsync<T>(Func<FolioDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FolioDbContext>();
        return await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
