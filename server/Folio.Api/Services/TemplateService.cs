using System.Text.Json;
using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Reusable page templates: capture a page's blocks, list, and instantiate.</summary>
public class TemplateService(FolioDbContext db, ICurrentMemberAccessor current, PageService pages)
{
    private static DateTime Now => DateTime.UtcNow;
    private CurrentMember? Member => current.Member;

    private sealed record StoredBlock(Guid Id, Guid? ParentBlockId, string Type, int Position, string Content);

    /// <summary>Create a template capturing a page's title, icon, and block set.</summary>
    public async Task<ServiceResult<TemplateResponse>> CreateFromPageAsync(Guid pageId, CreateTemplateRequest request, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<TemplateResponse>.NotFound("Page not found.");
        }

        if (Guard<TemplateResponse>(PageAuthorization.CanRead(Member, page.WorkspaceId, page.Visibility)) is { } denied)
        {
            return denied;
        }

        var blocks = await db.Blocks
            .Where(b => b.PageId == pageId)
            .OrderBy(b => b.Position)
            .Select(b => new StoredBlock(b.Id, b.ParentBlockId, b.Type.ToString(), b.Position, b.Content))
            .ToListAsync(ct);

        var template = new PageTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = page.WorkspaceId,
            Name = request.Name!.Trim(),
            Description = request.Description,
            SourceTitle = page.Title,
            SourceIcon = page.Icon,
            BlocksJson = JsonSerializer.Serialize(blocks),
            BlockCount = blocks.Count,
            CreatedByMemberId = Member?.MemberId,
            CreatedByName = Member?.Name,
            CreatedAt = Now,
        };

        db.PageTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ServiceResult<TemplateResponse>.Ok(ToResponse(template));
    }

    public async Task<ServiceResult<IReadOnlyList<TemplateResponse>>> ListAsync(Guid workspaceId, CancellationToken ct)
    {
        if (Member?.WorkspaceId != workspaceId)
        {
            return ServiceResult<IReadOnlyList<TemplateResponse>>.NotFound("Workspace not found.");
        }

        var templates = await db.PageTemplates
            .Where(t => t.WorkspaceId == workspaceId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<TemplateResponse>>.Ok(templates.Select(ToResponse).ToList());
    }

    /// <summary>Create a new page from a template.</summary>
    public async Task<ServiceResult<PageDetailResponse>> InstantiateAsync(Guid workspaceId, Guid templateId, InstantiateTemplateRequest request, CancellationToken ct)
    {
        var member = Member;
        if (member is null || member.WorkspaceId != workspaceId)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Workspace not found.");
        }

        if (member.Role == MemberRole.Viewer)
        {
            return ServiceResult<PageDetailResponse>.Forbidden("Viewers cannot create pages.");
        }

        var template = await db.PageTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.WorkspaceId == workspaceId, ct);
        if (template is null)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Template not found.");
        }

        if (request.ParentId is Guid parentId &&
            !await db.Pages.AnyAsync(p => p.Id == parentId && p.WorkspaceId == workspaceId, ct))
        {
            return ServiceResult<PageDetailResponse>.Invalid("Parent page not found in this workspace.");
        }

        // Create the page through PageService so sibling positions stay consistent.
        var created = await pages.CreateAsync(
            workspaceId,
            new CreatePageRequest(template.SourceTitle, request.ParentId, null, template.SourceIcon),
            ct);
        if (created.Status != OperationStatus.Success)
        {
            return created;
        }

        var newPageId = created.Value!.Id;
        var stored = JsonSerializer.Deserialize<List<StoredBlock>>(template.BlocksJson) ?? [];
        var blockIdMap = stored.ToDictionary(b => b.Id, _ => Guid.NewGuid());
        var when = Now;

        var blocks = stored.Select(b => new Block
        {
            Id = blockIdMap[b.Id],
            PageId = newPageId,
            ParentBlockId = b.ParentBlockId is Guid pb && blockIdMap.TryGetValue(pb, out var mapped) ? mapped : null,
            Type = Enum.Parse<BlockType>(b.Type),
            Position = b.Position,
            Content = b.Content,
            CreatedAt = when,
            UpdatedAt = when,
        }).ToList();
        db.Blocks.AddRange(blocks);
        await db.SaveChangesAsync(ct);

        return await pages.GetDetailAsync(newPageId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid workspaceId, Guid templateId, CancellationToken ct)
    {
        var member = Member;
        if (member is null || member.WorkspaceId != workspaceId)
        {
            return ServiceResult<bool>.NotFound("Workspace not found.");
        }

        if (member.Role == MemberRole.Viewer)
        {
            return ServiceResult<bool>.Forbidden("Viewers cannot delete templates.");
        }

        var template = await db.PageTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.WorkspaceId == workspaceId, ct);
        if (template is null)
        {
            return ServiceResult<bool>.NotFound("Template not found.");
        }

        db.PageTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    private static TemplateResponse ToResponse(PageTemplate t) => new(
        t.Id, t.WorkspaceId, t.Name, t.Description, t.SourceTitle, t.SourceIcon, t.BlockCount, t.CreatedByName, t.CreatedAt);

    private static ServiceResult<T>? Guard<T>(AccessResult access) => access switch
    {
        AccessResult.Allowed => null,
        AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have access to this page."),
        _ => ServiceResult<T>.NotFound("Page not found."),
    };
}
