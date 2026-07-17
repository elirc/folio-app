using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the EF Core SQLite <see cref="FolioDbContext"/>.</summary>
    public static IServiceCollection AddFolioInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FolioDbContext>(options => options
            .UseSqlite(connectionString)
            // Blocks are a required navigation off Page, which now has a
            // soft-delete query filter; this interaction warning is expected.
            .ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        return services;
    }
}
