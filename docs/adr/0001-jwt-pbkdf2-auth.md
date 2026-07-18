# 0001 — JWT bearer auth with in-house PBKDF2 password hashing

**Status:** Accepted

## Context

Every non-public endpoint needs an authenticated caller whose workspace and role drive
authorization. The seeder (in `Folio.Infrastructure`) must be able to hash the sample
members' passwords without depending on the web project, and we want no third-party
password-hashing dependency.

## Decision

- **Stateless JWT bearer tokens.** `POST /api/auth/login` verifies credentials and issues
  an HS256 JWT (`JwtTokenService`) whose claims carry the member id, email, name, role,
  and a custom `workspaceId`. Tokens expire after `Jwt:ExpiryHours` (default 12h).
  `ICurrentMemberAccessor` reconstructs the `CurrentMember` from those claims per request.
- **In-house PBKDF2.** `Folio.Domain/Security/PasswordHasher` uses `Rfc2898DeriveBytes`
  (SHA-256, 100k iterations, 16-byte salt, 32-byte key). The stored hash is
  self-describing — `iterations.base64(salt).base64(key)` — and verification uses a
  fixed-time comparison. It lives in **Domain** so both the API and the seeder can use it
  with no framework dependency.

## Consequences

- No server-side session store; the token is the whole auth state. Revocation before
  expiry isn't supported (acceptable for this app's scope).
- The signing key lives in configuration (`Jwt:Key`) — fine for dev; a real deployment
  must supply a strong secret out-of-band.
- Login returns the same `401` for unknown-email and wrong-password so accounts can't be
  probed.
