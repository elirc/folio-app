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
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentMention> CommentMentions => Set<CommentMention>();
    public DbSet<PageLink> PageLinks => Set<PageLink>();
    public DbSet<PageTemplate> PageTemplates => Set<PageTemplate>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Notification> Notifications => Set<Notification>();

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

        modelBuilder.Entity<Comment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Body).IsRequired().HasMaxLength(4000);
            e.Property(c => c.AuthorName).IsRequired().HasMaxLength(200);

            e.HasOne(c => c.Page)
                .WithMany()
                .HasForeignKey(c => c.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Replies: restrict so a thread root isn't silently cascade-deleted;
            // reply cleanup is handled explicitly in CommentService.
            e.HasOne(c => c.Parent)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(c => new { c.PageId, c.BlockId });
        });

        modelBuilder.Entity<CommentMention>(e =>
        {
            e.HasKey(m => m.Id);

            e.HasOne(m => m.Comment)
                .WithMany(c => c.Mentions)
                .HasForeignKey(m => m.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Member)
                .WithMany()
                .HasForeignKey(m => m.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => new { m.CommentId, m.MemberId }).IsUnique();
        });

        modelBuilder.Entity<PageLink>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.TargetTitle).HasMaxLength(400);

            // Only the source block is a real FK; deleting the block removes its
            // links (cascade). The target is a plain id so links can dangle.
            e.HasOne(l => l.SourceBlock)
                .WithMany()
                .HasForeignKey(l => l.SourceBlockId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(l => l.TargetPageId);
            e.HasIndex(l => l.SourcePageId);
        });

        modelBuilder.Entity<PageTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Description).HasMaxLength(1000);
            e.Property(t => t.SourceTitle).IsRequired().HasMaxLength(400);
            e.Property(t => t.SourceIcon).HasMaxLength(40);
            e.Property(t => t.BlocksJson).IsRequired();
            e.Property(t => t.CreatedByName).HasMaxLength(200);

            e.HasOne(t => t.Workspace)
                .WithMany()
                .HasForeignKey(t => t.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(t => t.WorkspaceId);
        });

        modelBuilder.Entity<Activity>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.ActorName).IsRequired().HasMaxLength(200);
            e.Property(a => a.Type).IsRequired().HasMaxLength(40);
            e.Property(a => a.PageTitle).HasMaxLength(400);
            e.Property(a => a.Summary).IsRequired().HasMaxLength(500);
            e.HasIndex(a => new { a.WorkspaceId, a.CreatedAt });
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Type).IsRequired().HasMaxLength(40);
            e.Property(n => n.PageTitle).HasMaxLength(400);
            e.Property(n => n.Summary).IsRequired().HasMaxLength(500);

            e.HasOne(n => n.Activity)
                .WithMany()
                .HasForeignKey(n => n.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(n => new { n.RecipientMemberId, n.IsRead });
        });
    }
}
