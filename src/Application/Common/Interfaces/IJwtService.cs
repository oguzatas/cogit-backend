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
}
