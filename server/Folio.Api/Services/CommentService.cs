using System.Text.RegularExpressions;
using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Page/block comment threads with resolve state and parsed @mentions.</summary>
public partial class CommentService(FolioDbContext db, ICurrentMemberAccessor current, ActivityService activity)
{
    private static DateTime Now => DateTime.UtcNow;
    private CurrentMember? Member => current.Member;

    // Rich-mention token: @[Display Name](member-guid). The client's mention
    // picker inserts these; we extract the guids and store them as references.
    [GeneratedRegex(@"@\[[^\]]+\]\((?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex MentionRegex();

    /// <summary>All comments on a page (page-level and block-level), oldest first.</summary>
    public async Task<ServiceResult<IReadOnlyList<CommentResponse>>> GetForPageAsync(Guid pageId, CancellationToken ct)
    {
        var page = await PageAuthAsync(pageId, ct);
        if (page is null)
        {
            return ServiceResult<IReadOnlyList<CommentResponse>>.NotFound("Page not found.");
        }

        if (ReadGuard<IReadOnlyList<CommentResponse>>(page.WorkspaceId, page.Visibility) is { } denied)
        {
            return denied;
        }

        // Capped for the pagination audit — a page's most recent 500 comments.
        var comments = await db.Comments
            .Where(c => c.PageId == pageId)
            .Include(c => c.Mentions)
                .ThenInclude(m => m.Member)
            .OrderBy(c => c.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<CommentResponse>>.Ok(comments.Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<CommentResponse>> CreateAsync(Guid pageId, CreateCommentRequest request, CancellationToken ct)
    {
        var page = await PageAuthAsync(pageId, ct);
        if (page is null)
        {
            return ServiceResult<CommentResponse>.NotFound("Page not found.");
        }

        // Commenting only needs read access (viewers can comment).
        if (ReadGuard<CommentResponse>(page.WorkspaceId, page.Visibility) is { } denied)
        {
            return denied;
        }

        var member = Member!;

        if (request.BlockId is Guid blockId &&
            !await db.Blocks.AnyAsync(b => b.Id == blockId && b.PageId == pageId, ct))
        {
            return ServiceResult<CommentResponse>.Invalid("Block not found on this page.");
        }

        if (request.ParentCommentId is Guid parentId &&
            !await db.Comments.AnyAsync(c => c.Id == parentId && c.PageId == pageId, ct))
        {
            return ServiceResult<CommentResponse>.Invalid("Parent comment not found on this page.");
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            BlockId = request.BlockId,
            ParentCommentId = request.ParentCommentId,
            AuthorMemberId = member.MemberId,
            AuthorName = member.Name,
            Body = request.Body!.Trim(),
            CreatedAt = Now,
            UpdatedAt = Now,
        };

        var mentionedIds = await ParseMentionsAsync(page.WorkspaceId, comment.Body, ct);
        foreach (var memberId in mentionedIds)
        {
            comment.Mentions.Add(new CommentMention
            {
                Id = Guid.NewGuid(),
                CommentId = comment.Id,
                MemberId = memberId,
            });
        }

        db.Comments.Add(comment);

        // Record the activity and fan notifications out to mentions, the page
        // author, and prior commenters — all in this same transaction.
        var record = activity.Add(page.WorkspaceId, member, ActivityTypes.CommentCreated, pageId, page.Title, $"commented on \"{page.Title}\"", comment.Id);
        await activity.FanOutCommentAsync(record, pageId, page.CreatedByMemberId, member.MemberId, mentionedIds, ct);

        await db.SaveChangesAsync(ct);

        // Reload mention member names for the response.
        await db.Entry(comment).Collection(c => c.Mentions).Query().Include(m => m.Member).LoadAsync(ct);
        return ServiceResult<CommentResponse>.Ok(ToResponse(comment));
    }

    public async Task<ServiceResult<CommentResponse>> SetResolvedAsync(Guid commentId, bool resolved, CancellationToken ct)
    {
        var comment = await db.Comments
            .Include(c => c.Mentions).ThenInclude(m => m.Member)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
        {
            return ServiceResult<CommentResponse>.NotFound("Comment not found.");
        }

        var page = await PageAuthAsync(comment.PageId, ct);
        if (page is null)
        {
            return ServiceResult<CommentResponse>.NotFound("Page not found.");
        }

        if (ReadGuard<CommentResponse>(page.WorkspaceId, page.Visibility) is { } denied)
        {
            return denied;
        }

        comment.IsResolved = resolved;
        comment.ResolvedAt = resolved ? Now : null;
        comment.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<CommentResponse>.Ok(ToResponse(comment));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
        {
            return ServiceResult<bool>.NotFound("Comment not found.");
        }

        var page = await PageAuthAsync(comment.PageId, ct);
        if (page is null)
        {
            return ServiceResult<bool>.NotFound("Page not found.");
        }

        // The author may always delete their own comment; otherwise page write
        // access is required.
        var member = Member;
        var isAuthor = member is not null && comment.AuthorMemberId == member.MemberId;
        if (!isAuthor)
        {
            if (WriteGuard<bool>(page.WorkspaceId, page.Visibility, page.Permission) is { } denied)
            {
                return denied;
            }
        }

        // Remove the thread: this comment plus any replies (Restrict FK).
        var replies = await db.Comments.Where(c => c.ParentCommentId == commentId).ToListAsync(ct);
        db.Comments.RemoveRange(replies);
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    // ---- helpers ----

    /// <summary>Extracts mention tokens and keeps only ids that are members of the workspace.</summary>
    private async Task<List<Guid>> ParseMentionsAsync(Guid workspaceId, string body, CancellationToken ct)
    {
        var ids = MentionRegex().Matches(body)
            .Select(m => Guid.TryParse(m.Groups["id"].Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Members
            .Where(m => m.WorkspaceId == workspaceId && ids.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);
    }

    private static CommentResponse ToResponse(Comment c) => new(
        c.Id,
        c.PageId,
        c.BlockId,
        c.ParentCommentId,
        c.AuthorMemberId,
        c.AuthorName,
        c.Body,
        c.IsResolved,
        c.ResolvedAt,
        c.Mentions
            .Select(m => new MentionDto(m.MemberId, m.Member?.Name ?? string.Empty))
            .ToList(),
        c.CreatedAt,
        c.UpdatedAt);

    // ---- authorization ----

    private sealed record PageAuth(Guid WorkspaceId, PageVisibility Visibility, SharePermission Permission, Guid? CreatedByMemberId, string Title);

    private Task<PageAuth?> PageAuthAsync(Guid pageId, CancellationToken ct) =>
        db.Pages
            .Where(p => p.Id == pageId)
            .Select(p => new PageAuth(p.WorkspaceId, p.Visibility, p.Permission, p.CreatedByMemberId, p.Title))
            .FirstOrDefaultAsync(ct);

    private ServiceResult<T>? ReadGuard<T>(Guid workspaceId, PageVisibility visibility) =>
        PageAuthorization.CanRead(Member, workspaceId, visibility) switch
        {
            AccessResult.Allowed => null,
            AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have access to this page."),
            _ => ServiceResult<T>.NotFound("Page not found."),
        };

    private ServiceResult<T>? WriteGuard<T>(Guid workspaceId, PageVisibility visibility, SharePermission permission) =>
        PageAuthorization.CanWrite(Member, workspaceId, visibility, permission) switch
        {
            AccessResult.Allowed => null,
            AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have permission to delete this comment."),
            _ => ServiceResult<T>.NotFound("Page not found."),
        };
}
