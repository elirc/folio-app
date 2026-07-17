using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Folio.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c>. Having it means the EF tooling
/// builds a DbContext directly instead of executing the API's Program startup
/// (which would otherwise run migrate/seed during migration generation).
/// </summary>
public class FolioDbContextFactory : IDesignTimeDbContextFactory<FolioDbContext>
{
    public FolioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FolioDbContext>()
            .UseSqlite("Data Source=folio.db")
            .Options;

        return new FolioDbContext(options);
    }
}
