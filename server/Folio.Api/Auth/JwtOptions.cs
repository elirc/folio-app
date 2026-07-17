namespace Folio.Api.Auth;

/// <summary>JWT signing/validation settings bound from the "Jwt" config section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key (must be at least 32 bytes for HS256).</summary>
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "folio";
    public string Audience { get; set; } = "folio-client";

    /// <summary>Token lifetime in hours.</summary>
    public int ExpiryHours { get; set; } = 12;
}
