using System.Text.Json;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.Security;

namespace Folio.Infrastructure.Persistence;

/// <summary>Idempotent development seed: two workspaces with members, page trees, and blocks.</summary>
public static class DbSeeder
{
    // Stable UTC instant so seeds are deterministic across runs and tests.
    private static readonly DateTime Seeded = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>All seeded members share this password for local login/testing.</summary>
    public const string DefaultPassword = "password";

    public static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid GettingStartedId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid InstallationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static readonly Guid ConfigurationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    public static readonly Guid EngineeringId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
    public static readonly Guid ArchitectureId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");
    public static readonly Guid RunbooksId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006");
    public static readonly Guid ProductId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007");

    // Seeded login emails (workspace "Acme Docs"): Owner / Editor / Viewer.
    public const string OwnerEmail = "ada@acme.test";
    public const string EditorEmail = "grace@acme.test";
    public const string ViewerEmail = "linus@acme.test";

    // A second, isolated workspace used to exercise cross-workspace access rules.
    public static readonly Guid GlobexWorkspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GlobexHomeId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    public const string GlobexOwnerEmail = "eve@globex.test";

    public static void Seed(FolioDbContext db)
    {
        if (db.Workspaces.Any())
        {
            return;
        }

        var workspace = new Workspace
        {
            Id = WorkspaceId,
            Name = "Acme Docs",
            Slug = "acme-docs",
            CreatedAt = Seeded,
            UpdatedAt = Seeded,
            Members =
            [
                Member("Ada Lovelace", OwnerEmail, MemberRole.Owner),
                Member("Grace Hopper", EditorEmail, MemberRole.Editor),
                Member("Linus Torvalds", ViewerEmail, MemberRole.Viewer),
            ],
        };

        db.Workspaces.Add(workspace);

        // A second workspace with its own owner — never visible to Acme members.
        var globex = new Workspace
        {
            Id = GlobexWorkspaceId,
            Name = "Globex",
            Slug = "globex",
            CreatedAt = Seeded,
            UpdatedAt = Seeded,
            Members = [Member("Eve Polastri", GlobexOwnerEmail, MemberRole.Owner)],
        };
        db.Workspaces.Add(globex);
        var globexHome = new Page
        {
            Id = GlobexHomeId,
            WorkspaceId = GlobexWorkspaceId,
            ParentId = null,
            Title = "Globex Home",
            Icon = "🏢",
            Position = 0,
            CreatedAt = Seeded,
            UpdatedAt = Seeded,
        };
        db.Pages.Add(globexHome);

        // Root pages.
        var gettingStarted = Page(GettingStartedId, null, "Getting Started", "📘", 0);
        var engineering = Page(EngineeringId, null, "Engineering", "🛠️", 1);
        var product = Page(ProductId, null, "Product", "🚀", 2);

        // Showcase sharing + favorites in the seed.
        gettingStarted.IsFavorite = true;
        product.Visibility = Domain.Enums.PageVisibility.Public;
        product.Permission = Domain.Enums.SharePermission.View;
        product.PublicSlug = "acme-product-roadmap";

        // Children.
        var installation = Page(InstallationId, GettingStartedId, "Installation", "📦", 0);
        var configuration = Page(ConfigurationId, GettingStartedId, "Configuration", "⚙️", 1);
        var architecture = Page(ArchitectureId, EngineeringId, "Architecture", "🏗️", 0);
        var runbooks = Page(RunbooksId, EngineeringId, "Runbooks", "📓", 1);

        db.Pages.AddRange(gettingStarted, engineering, product, installation, configuration, architecture, runbooks);

        db.Blocks.AddRange(
            Block(GettingStartedId, BlockType.Heading, 0, new { text = "Welcome to Folio", level = 1 }),
            Block(GettingStartedId, BlockType.Paragraph, 1, new { text = "Folio is a Notion-style knowledge base. Pages nest into a tree and hold typed blocks." }),
            Block(GettingStartedId, BlockType.Todo, 2, new { text = "Create your first page", @checked = true }),
            Block(GettingStartedId, BlockType.Todo, 3, new { text = "Invite a teammate", @checked = false }),
            Block(GettingStartedId, BlockType.Quote, 4, new { text = "Docs are a love letter to your future self." }),
            Block(InstallationId, BlockType.Heading, 0, new { text = "Installation", level = 2 }),
            Block(InstallationId, BlockType.Bulleted, 1, new { text = "Install the .NET 10 SDK" }),
            Block(InstallationId, BlockType.Bulleted, 2, new { text = "Install Node 22 and pnpm" }),
            Block(InstallationId, BlockType.Code, 3, new { text = "pnpm install\npnpm dev", language = "bash" }),
            Block(ArchitectureId, BlockType.Heading, 0, new { text = "Architecture", level = 2 }),
            Block(ArchitectureId, BlockType.Paragraph, 1, new { text = "ASP.NET Core Web API + EF Core/SQLite backend, Vite + React frontend." }));

        db.SaveChanges();
    }

    private static Member Member(string name, string email, MemberRole role) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        Role = role,
        PasswordHash = PasswordHasher.Hash(DefaultPassword),
        CreatedAt = Seeded,
    };

    private static Page Page(Guid id, Guid? parentId, string title, string icon, int position) => new()
    {
        Id = id,
        WorkspaceId = WorkspaceId,
        ParentId = parentId,
        Title = title,
        Icon = icon,
        Position = position,
        CreatedAt = Seeded,
        UpdatedAt = Seeded,
    };

    private static Block Block(Guid pageId, BlockType type, int position, object content) => new()
    {
        Id = Guid.NewGuid(),
        PageId = pageId,
        Type = type,
        Position = position,
        Content = JsonSerializer.Serialize(content),
        CreatedAt = Seeded,
        UpdatedAt = Seeded,
    };
}
