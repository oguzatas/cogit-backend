using System.Security.Claims;
using backend.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace backend.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public int? DomainUserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("domain_user_id");
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
