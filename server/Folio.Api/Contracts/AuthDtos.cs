using System.ComponentModel.DataAnnotations;
using Folio.Domain.Enums;

namespace Folio.Api.Contracts;

// Validation attributes stay on the constructor parameters (records); required
// fields are nullable so a missing value yields a 400 rather than a bogus default.

/// <summary>Credentials for JWT login.</summary>
public record LoginRequest(
    [Required][EmailAddress] string? Email,
    [Required] string? Password);

/// <summary>The authenticated member's identity (no secrets).</summary>
public record MemberResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Email,
    MemberRole Role);

/// <summary>A successful login: a bearer token, its expiry, and the member.</summary>
public record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    MemberResponse Member);
