using System.Security.Claims;
using Folio.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Folio.Api.Auth;

/// <summary>The authenticated caller, resolved from the JWT bearer claims.</summary>
public record CurrentMember(Guid MemberId, Guid WorkspaceId, MemberRole Role, string Email, string Name);

/// <summary>Exposes the authenticated member (if any) for the current request.</summary>
public interface ICurrentMemberAccessor
{
    /// <summary>The current member, or null when the request is anonymous.</summary>
    CurrentMember? Member { get; }
}

/// <summary>Custom claim types used by Folio's JWTs, alongside the standard ones.</summary>
public static class FolioClaims
{
    public const string WorkspaceId = "workspaceId";
}

/// <summary>Reads the current member out of <see cref="HttpContext.User"/>.</summary>
public class CurrentMemberAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentMemberAccessor
{
    public CurrentMember? Member
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var workspaceId = user.FindFirstValue(FolioClaims.WorkspaceId);
            var role = user.FindFirstValue(ClaimTypes.Role);
            if (!Guid.TryParse(id, out var memberId)
                || !Guid.TryParse(workspaceId, out var wsId)
                || !Enum.TryParse<MemberRole>(role, out var memberRole))
            {
                return null;
            }

            return new CurrentMember(
                memberId,
                wsId,
                memberRole,
                user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                user.FindFirstValue(ClaimTypes.Name) ?? string.Empty);
        }
    }
}
