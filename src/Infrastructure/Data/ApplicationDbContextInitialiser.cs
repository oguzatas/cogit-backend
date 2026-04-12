using System.Security.Claims;
using backend.Domain.Constants;
using backend.Domain.Entities;
using backend.Domain.Enums;
using backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace backend.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while running database migrations.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // ── 1. Ensure Identity roles exist ────────────────────────────────────
        foreach (var roleName in new[] { Roles.SuperAdmin, Roles.TenantStaff })
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // ── 2. Seed SuperAdmin user ───────────────────────────────────────────
        const string email    = "oguzhanatas37@gmail.com";
        const string password = "12345678aA.";

        var identityUser = await _userManager.FindByEmailAsync(email);

        if (identityUser is null)
        {
            identityUser = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(identityUser, password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create SuperAdmin Identity user: {Errors}", errors);
                return;
            }

            await _userManager.AddToRoleAsync(identityUser, Roles.SuperAdmin);

            // ── 3. Create matching AppUser domain record ──────────────────────
            var domainUser = new AppUser
            {
                Email        = email,
                PasswordHash = identityUser.PasswordHash!,
                Role         = UserRole.SuperAdmin,
                TenantId     = null,   // SuperAdmin is platform-wide
                IsDeleted    = false
            };

            _context.AppUsers.Add(domainUser);
            await _context.SaveChangesAsync(CancellationToken.None);

            // ── 4. Store domain_user_id as a persistent Identity claim ────────
            // This claim is included in every JWT so ICurrentUserService.DomainUserId works.
            await _userManager.AddClaimsAsync(identityUser,
            [
                new Claim("domain_user_id", domainUser.Id.ToString()),
                new Claim("role",           Roles.SuperAdmin)
            ]);

            _logger.LogInformation("SuperAdmin seeded → Identity: {IdentityId}, Domain: {DomainId}",
                identityUser.Id, domainUser.Id);
        }
    }
}
