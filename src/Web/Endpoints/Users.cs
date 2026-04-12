using backend.Application.Common.Interfaces;
using backend.Infrastructure.Data;
using backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Web.Endpoints;

/// <summary>
/// Authentication endpoints.
///
/// POST /api/Users/login    [Public]  — returns JWT in body + refresh token in HttpOnly cookie
/// POST /api/Users/refresh  [Public]  — rotates refresh token; returns new JWT
/// POST /api/Users/logout   [Bearer]  — revokes refresh token + clears cookie
/// </summary>
public class Users : IEndpointGroup
{
    private const string RefreshTokenCookie = "refresh_token";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(Login,   "login")  .AllowAnonymous();
        groupBuilder.MapPost(Refresh, "refresh").AllowAnonymous();
        groupBuilder.MapPost(Logout,  "logout") .RequireAuthorization();
    }

    // ── POST /api/Users/login ─────────────────────────────────────────────────

    [EndpointSummary("Login")]
    [EndpointDescription(
        "Validates credentials. Returns a JWT access token in the response body " +
        "and a refresh token as an HttpOnly cookie.")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Login(
        UserManager<ApplicationUser>  userManager,
        IJwtService                   jwtService,
        ApplicationDbContext          db,
        IOptions<JwtSettings>         jwtOptions,
        IWebHostEnvironment           env,
        LoginRequest                  request,
        HttpContext                   httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return TypedResults.Unauthorized();

        var (accessToken, refreshTokenValue) =
            await IssueTokenPair(user, userManager, jwtService, db, jwtOptions.Value, httpContext, env);

        return TypedResults.Ok(new LoginResponse(accessToken, jwtOptions.Value.AccessTokenExpiryMinutes * 60));
    }

    // ── POST /api/Users/refresh ───────────────────────────────────────────────

    [EndpointSummary("Refresh Access Token")]
    [EndpointDescription(
        "Reads the refresh token from the HttpOnly cookie, validates it, " +
        "rotates it (old token revoked, new one issued), and returns a new JWT.")]
    public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Refresh(
        UserManager<ApplicationUser>  userManager,
        IJwtService                   jwtService,
        ApplicationDbContext          db,
        IOptions<JwtSettings>         jwtOptions,
        IWebHostEnvironment           env,
        HttpContext                   httpContext)
    {
        var cookieToken = httpContext.Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(cookieToken))
            return TypedResults.Unauthorized();

        var stored = await db.RefreshTokens
            .Include(r => r.IdentityUser)
            .FirstOrDefaultAsync(r => r.Token == cookieToken);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTimeOffset.UtcNow)
            return TypedResults.Unauthorized();

        // Revoke the consumed token (rotation — one-time use).
        stored.IsRevoked = true;
        stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var (accessToken, _) =
            await IssueTokenPair(stored.IdentityUser, userManager, jwtService, db, jwtOptions.Value, httpContext, env);

        return TypedResults.Ok(new LoginResponse(accessToken, jwtOptions.Value.AccessTokenExpiryMinutes * 60));
    }

    // ── POST /api/Users/logout ────────────────────────────────────────────────

    [EndpointSummary("Logout")]
    [EndpointDescription("Revokes the refresh token cookie and clears it from the browser.")]
    public static async Task<Ok> Logout(
        ApplicationDbContext db,
        HttpContext          httpContext)
    {
        var cookieToken = httpContext.Request.Cookies[RefreshTokenCookie];

        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            var stored = await db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == cookieToken && !r.IsRevoked);

            if (stored is not null)
            {
                stored.IsRevoked = true;
                stored.RevokedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        httpContext.Response.Cookies.Delete(RefreshTokenCookie);
        return TypedResults.Ok();
    }

    // ── Shared helper ─────────────────────────────────────────────────────────

    private static async Task<(string AccessToken, string RefreshTokenValue)> IssueTokenPair(
        ApplicationUser              user,
        UserManager<ApplicationUser> userManager,
        IJwtService                  jwtService,
        ApplicationDbContext         db,
        JwtSettings                  settings,
        HttpContext                  httpContext,
        IWebHostEnvironment          env)
    {
        // Load persistent claims: domain_user_id, tenant_id (stored via seeder/registration).
        var userClaims = await userManager.GetClaimsAsync(user);
        var roles      = await userManager.GetRolesAsync(user);
        var role       = roles.FirstOrDefault() ?? string.Empty;

        var domainUserId = userClaims.FirstOrDefault(c => c.Type == "domain_user_id")?.Value;
        var tenantId     = userClaims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;

        var accessToken = jwtService.GenerateAccessToken(
            identityUserId: user.Id,
            email:          user.Email!,
            role:           role,
            domainUserId:   int.TryParse(domainUserId, out var duid) ? duid : null,
            tenantId:       int.TryParse(tenantId, out var tid)      ? tid  : null);

        // Generate + persist refresh token.
        var refreshTokenValue = jwtService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            IdentityUserId = user.Id,
            Token          = refreshTokenValue,
            ExpiresAt      = DateTimeOffset.UtcNow.AddDays(settings.RefreshTokenExpiryDays),
            IsRevoked      = false,
            CreatedAt      = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        // Append HttpOnly cookie — SameSite=None in dev (cross-origin localhost),
        // SameSite=Strict + Secure in production.
        var isDev = env.IsDevelopment();

        httpContext.Response.Cookies.Append(RefreshTokenCookie, refreshTokenValue, new CookieOptions
        {
            HttpOnly = true,
            Secure   = !isDev,
            SameSite = isDev ? SameSiteMode.None : SameSiteMode.Strict,
            MaxAge   = TimeSpan.FromDays(settings.RefreshTokenExpiryDays),
            Path     = "/api/Users"   // cookie only sent to auth endpoints
        });

        return (accessToken, refreshTokenValue);
    }
}

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, int ExpiresIn);
