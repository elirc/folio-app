using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence;

public class FolioDbContext(DbContextOptions<FolioDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Block> Blocks => Set<Block>();

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
            e.HasIndex(m => new { m.WorkspaceId, m.Email }).IsUnique();
        });

        modelBuilder.Entity<Page>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired().HasMaxLength(400);
            e.Property(p => p.Icon).HasMaxLength(40);

            // Self-referencing tree. Restrict so deleting a parent never silently
            // cascades away children in the database — the app handles subtree
            // removal explicitly (soft-delete arrives in a later sprint).
            e.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => new { p.WorkspaceId, p.ParentId, p.Position });
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

            e.HasIndex(b => new { b.PageId, b.Position });
        });
    }
}
