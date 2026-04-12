namespace backend.Domain.Entities;

/// <summary>
/// A configurable, multi-use invite token that allows people to self-register as
/// TenantEmployees under a specific Tenant and Department (Flow B).
///
/// Lifecycle:
///   Active   — not revoked, not expired, under usage limit.
///   Exhausted — UsageCount has reached MaxUses.
///   Expired  — current time is past ExpiresAt.
///   Revoked  — explicitly disabled by an admin via RevokeInviteCode.
/// </summary>
public class InviteCode : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int DepartmentId { get; set; }

    /// <summary>Cryptographically random token shared with invitees (URL-safe).</summary>
    public string Code { get; set; } = default!;

    /// <summary>How many times this code has been successfully redeemed.</summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Maximum number of redemptions allowed.
    /// NULL means unlimited (no cap).
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>NULL means the code never expires on its own.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>True when an admin has explicitly disabled this code.</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Populated when IsRevoked is set to true.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Department Department { get; set; } = default!;

    // ── Computed helpers ──────────────────────────────────────────────────────

    /// <summary>True if the code can still be redeemed right now.</summary>
    public bool IsActive =>
        !IsRevoked &&
        !IsDeleted &&
        (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow) &&
        (MaxUses is null || UsageCount < MaxUses);
}
