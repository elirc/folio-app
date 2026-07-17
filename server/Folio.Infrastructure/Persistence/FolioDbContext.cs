using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence;

public class FolioDbContext(DbContextOptions<FolioDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<PageVersion> PageVersions => Set<PageVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Name).IsRequired().HasMaxLength(200);
            e.Property(w => w.Slug).IsRequired().HasMaxLength(120);
            e.HasIndex(w => w.Slug).IsUnique();

            e.HasMany(w => w.Members)
                .WithOne(m => m.Workspace!)
                .HasForeignKey(m => m.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(w => w.Pages)
                .WithOne(p => p.Workspace!)
                .HasForeignKey(p => p.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Member>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).IsRequired().HasMaxLength(200);
            e.Property(m => m.Email).IsRequired().HasMaxLength(320);
            e.Property(m => m.Role)
                .HasConversion<string>()
                .HasMaxLength(20);
            e.Property(m => m.PasswordHash).IsRequired();
            e.HasIndex(m => new { m.WorkspaceId, m.Email }).IsUnique();
            // Email is the login identifier; unique across all workspaces so a
            // login request resolves to exactly one member.
            e.HasIndex(m => m.Email).IsUnique();
        });

        modelBuilder.Entity<Page>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired().HasMaxLength(400);
            e.Property(p => p.Icon).HasMaxLength(40);
            e.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Permission).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.PublicSlug).HasMaxLength(40);

            // Self-referencing tree. Restrict so deleting a parent never silently
            // cascades away children in the database — subtree removal is handled
            // explicitly (and soft-delete keeps rows around anyway).
            e.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => new { p.WorkspaceId, p.ParentId, p.Position });
            e.HasIndex(p => p.PublicSlug)
                .IsUnique()
                .HasFilter("\"PublicSlug\" IS NOT NULL");

            // Soft-deleted pages are hidden from every query unless it opts out
            // with IgnoreQueryFilters() (used by the trash/restore paths).
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<Block>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Type)
                .HasConversion<string>()
                .HasMaxLength(20);
            e.Property(b => b.Content).IsRequired();

            e.HasOne(b => b.Page)
                .WithMany(p => p.Blocks)
                .HasForeignKey(b => b.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing block tree (children under a Toggle). Restrict so
            // deleting a parent never silently cascades away children in the
            // database — subtree removal is handled explicitly in BlockService.
            e.HasOne(b => b.Parent)
                .WithMany(b => b.Children)
                .HasForeignKey(b => b.ParentBlockId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(b => new { b.PageId, b.ParentBlockId, b.Position });
        });

        modelBuilder.Entity<PageVersion>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Title).IsRequired().HasMaxLength(400);
            e.Property(v => v.Icon).HasMaxLength(40);
            e.Property(v => v.BlocksJson).IsRequired();
            e.Property(v => v.CreatedByName).HasMaxLength(200);
            e.Property(v => v.Label).HasMaxLength(120);

            e.HasOne(v => v.Page)
                .WithMany()
                .HasForeignKey(v => v.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(v => new { v.PageId, v.VersionNumber }).IsUnique();
        });
    }
}
