using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Folio.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Folio.Api.Auth;

/// <summary>Issues signed JWT bearer tokens for authenticated members.</summary>
public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) Issue(Member member)
    {
        var expiresAt = DateTime.UtcNow.AddHours(_options.ExpiryHours);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, member.Id.ToString()),
            new Claim(ClaimTypes.Email, member.Email),
            new Claim(ClaimTypes.Name, member.Name),
            new Claim(ClaimTypes.Role, member.Role.ToString()),
            new Claim(FolioClaims.WorkspaceId, member.WorkspaceId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
