namespace backend.Application.Common.Interfaces;

/// <summary>
/// Provides identity context for the currently authenticated user.
/// TenantId == null means the caller is a System Admin with no tenant scope.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    int? TenantId { get; }
}
