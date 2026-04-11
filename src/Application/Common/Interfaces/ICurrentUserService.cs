namespace backend.Application.Common.Interfaces;

/// <summary>
/// Provides identity context for the currently authenticated user.
/// TenantId == null means the caller is a System Admin with no tenant scope.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>ASP.NET Identity sub claim (GUID string).</summary>
    string? UserId { get; }

    /// <summary>
    /// The tenant this user belongs to.
    /// NULL for System Admins — they are not scoped to any tenant.
    /// Sourced from the <c>tenant_id</c> JWT claim.
    /// </summary>
    int? TenantId { get; }

    /// <summary>
    /// The primary-key of this user's row in the domain <c>Users</c> table.
    /// Populated as the <c>domain_user_id</c> JWT claim during login.
    /// NULL until the login flow writes the claim.
    /// </summary>
    int? DomainUserId { get; }
}
