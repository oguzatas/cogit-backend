using System.Reflection;
using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    // ── Identity & Tenant ─────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    // 'new' intentionally hides IdentityDbContext.Users (DbSet<ApplicationUser>).
    // Our domain User entity maps to the "Users" table; Identity maps to "AspNetUsers".
    public new DbSet<User> Users => Set<User>();

    // ── Test Builder (Global) ─────────────────────────────────────────────────
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    // ── Rules Engine (Global) ─────────────────────────────────────────────────
    public DbSet<BranchingRule> BranchingRules => Set<BranchingRule>();
    public DbSet<ScoringScale> ScoringScales => Set<ScoringScale>();

    // ── Distribution ──────────────────────────────────────────────────────────
    public DbSet<TenantTestAccess> TenantTestAccesses => Set<TenantTestAccess>();

    // ── Execution (Tenant-Scoped) ─────────────────────────────────────────────
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ClientAnswer> ClientAnswers => Set<ClientAnswer>();
    public DbSet<AssignmentResult> AssignmentResults => Set<AssignmentResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyGlobalQueryFilters(builder);
    }

    /// <summary>
    /// Centralises all global query filters so the logic is in one place.
    ///
    /// Rules:
    ///   - Every entity filters out soft-deleted rows (!IsDeleted).
    ///   - Tenant-scoped entities additionally filter by TenantId when the
    ///     caller is not a System Admin (TenantId != null).
    ///   - Global entities (Test, Question, etc.) only filter by IsDeleted.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder builder)
    {
        // ── Global entities (IsDeleted only) ──────────────────────────────────
        builder.Entity<Test>()
            .HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<Question>()
            .HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<QuestionOption>()
            .HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<BranchingRule>()
            .HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<ScoringScale>()
            .HasQueryFilter(e => !e.IsDeleted);

        // ── Tenant root (IsDeleted only — no TenantId column on this entity) ──
        builder.Entity<Tenant>()
            .HasQueryFilter(e => !e.IsDeleted);

        // ── Tenant-scoped entities (IsDeleted + TenantId) ─────────────────────
        // When TenantId is null the caller is a System Admin — bypass tenant filter.
        builder.Entity<User>()
            .HasQueryFilter(e => !e.IsDeleted
                && (_currentUserService.TenantId == null
                    || e.TenantId == _currentUserService.TenantId));

        builder.Entity<TenantTestAccess>()
            .HasQueryFilter(e => !e.IsDeleted
                && (_currentUserService.TenantId == null
                    || e.TenantId == _currentUserService.TenantId));

        builder.Entity<Assignment>()
            .HasQueryFilter(e => !e.IsDeleted
                && (_currentUserService.TenantId == null
                    || e.TenantId == _currentUserService.TenantId));

        builder.Entity<ClientAnswer>()
            .HasQueryFilter(e => !e.IsDeleted
                && (_currentUserService.TenantId == null
                    || e.TenantId == _currentUserService.TenantId));

        builder.Entity<AssignmentResult>()
            .HasQueryFilter(e => !e.IsDeleted
                && (_currentUserService.TenantId == null
                    || e.TenantId == _currentUserService.TenantId));
    }
}
