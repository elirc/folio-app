using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the EF Core SQLite <see cref="FolioDbContext"/>.</summary>
    public static IServiceCollection AddFolioInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FolioDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
