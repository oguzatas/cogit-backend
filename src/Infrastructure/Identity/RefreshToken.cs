namespace backend.Infrastructure.Identity;

/// <summary>
/// Opaque refresh token stored server-side.
/// Sent to the client exclusively via an HttpOnly cookie — never in the response body.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>FK → AspNetUsers.Id</summary>
    public string IdentityUserId { get; set; } = default!;

    /// <summary>Cryptographically random 128-char hex string.</summary>
    public string Token { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public virtual ApplicationUser IdentityUser { get; set; } = default!;
}
