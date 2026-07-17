using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Security;
using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(FolioDbContext db, JwtTokenService tokens, ICurrentMemberAccessor current) : ControllerBase
{
    /// <summary>Exchange member credentials for a JWT bearer token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var member = await db.Members.FirstOrDefaultAsync(m => m.Email == email, ct);

        // Same message + status whether the email is unknown or the password is
        // wrong, so callers can't probe which accounts exist.
        if (member is null || !PasswordHasher.Verify(request.Password!, member.PasswordHash))
        {
            return Problem(statusCode: 401, detail: "Invalid email or password.");
        }

        var (token, expiresAt) = tokens.Issue(member);
        return Ok(new LoginResponse(token, expiresAt, ToResponse(member.Id, member.WorkspaceId, member.Name, member.Email, member.Role)));
    }

    /// <summary>The current bearer's identity.</summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<MemberResponse> Me()
    {
        var member = current.Member;
        return member is null
            ? Problem(statusCode: 401, detail: "Not authenticated.")
            : Ok(new MemberResponse(member.MemberId, member.WorkspaceId, member.Name, member.Email, member.Role));
    }

    private static MemberResponse ToResponse(Guid id, Guid workspaceId, string name, string email, Domain.Enums.MemberRole role) =>
        new(id, workspaceId, name, email, role);
}
