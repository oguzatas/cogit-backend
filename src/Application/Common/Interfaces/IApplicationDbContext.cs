using backend.Domain.Entities;

namespace backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // ── Tenant & Identity ─────────────────────────────────────────────────────
    DbSet<Tenant> Tenants { get; }
    DbSet<Department> Departments { get; }
    DbSet<AppUser> AppUsers { get; }
    DbSet<TenantEmployee> TenantEmployees { get; }
    DbSet<InviteCode> InviteCodes { get; }

    // ── Test Builder (Global) ─────────────────────────────────────────────────
    DbSet<Test> Tests { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }

    // ── Rules Engine (Global) ─────────────────────────────────────────────────
    DbSet<BranchingRule> BranchingRules { get; }
    DbSet<ScoringScale> ScoringScales { get; }

    // ── Distribution ──────────────────────────────────────────────────────────
    DbSet<TenantTestAccess> TenantTestAccesses { get; }

    // ── Execution (Tenant-Scoped) ─────────────────────────────────────────────
    DbSet<Assignment> Assignments { get; }
    DbSet<ClientAnswer> ClientAnswers { get; }
    DbSet<AssignmentResult> AssignmentResults { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
