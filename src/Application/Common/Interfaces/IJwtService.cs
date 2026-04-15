namespace backend.Application.Common.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// Generates a signed JWT access token with the standard claims payload.
    /// </summary>
    string GenerateAccessToken(
        string identityUserId,
        string email,
        string role,
        int?   domainUserId,
        int?   tenantId);

    /// <summary>
    /// Generates a cryptographically random opaque string for use as a refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Generates a short-lived guest JWT scoped to a single Assignment.
    /// The token carries <c>assignment_id</c> and <c>tenant_id</c> claims so that
    /// global query filters continue to work for the TenantEmployee without granting
    /// any staff/admin role.
    /// </summary>
    string GenerateGuestToken(int assignmentId, int tenantId, int expiryMinutes = 60);
}
