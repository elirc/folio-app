using System.Net.Http.Headers;
using System.Net.Http.Json;
using Folio.Api.Contracts;
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

    /// <summary>When set, overrides the per-user write rate-limit (used by the rate-limit test).</summary>
    public int? WritePermitLimit { get; init; }

    /// <summary>When set, overrides the rate-limit window in seconds (used by the recovery test).</summary>
    public int? WriteWindowSeconds { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // Development so the startup path also seeds sample data.
        builder.UseEnvironment("Development");

        if (WritePermitLimit is int limit)
        {
            builder.UseSetting("RateLimit:PermitLimit", limit.ToString());
        }

        if (WriteWindowSeconds is int window)
        {
            builder.UseSetting("RateLimit:WindowSeconds", window.ToString());
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<FolioDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<FolioDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// A client whose default Authorization header carries a bearer token for the
    /// given seeded member (default: the workspace Owner). Logs in over the real
    /// endpoint so the whole auth path is exercised.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string email = DbSeeder.OwnerEmail)
    {
        var client = CreateClient();
        var token = LoginAsync(client, email).GetAwaiter().GetResult();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = DbSeeder.DefaultPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        return body!.Token;
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
