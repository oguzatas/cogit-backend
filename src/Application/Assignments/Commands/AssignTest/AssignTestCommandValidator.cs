using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.AssignTest;

public class AssignTestCommandValidator : AbstractValidator<AssignTestCommand>
{
    private readonly IApplicationDbContext _context;

    public AssignTestCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        // ── Claim-sourced fields (set by endpoint, never from body) ───────────
        RuleFor(v => v.TenantId)
            .GreaterThan(0)
            .WithMessage("A valid TenantId is required. Ensure the tenant_id claim is present in the JWT.");

        RuleFor(v => v.AssignedByStaffId)
            .GreaterThan(0)
            .WithMessage("A valid AssignedByStaffId is required. Ensure the domain_user_id claim is present in the JWT.");

        // ── Body fields ───────────────────────────────────────────────────────
        RuleFor(v => v.TestId)
            .GreaterThan(0);

        RuleFor(v => v.ClientId)
            .GreaterThan(0);

        // ── Cross-entity rules (async DB checks) ──────────────────────────────

        // Rule 1: The Tenant must hold an active access grant for this Test.
        RuleFor(v => v)
            .MustAsync(TenantHasActiveTestAccess)
            .When(v => v.TenantId > 0 && v.TestId > 0)
            .WithName("TestId")
            .WithMessage(v =>
                $"Tenant {v.TenantId} does not have active access to Test {v.TestId}. " +
                 "Grant access via TenantTestAccess before assigning.")
            .WithErrorCode("Assignment.NoTestAccess");

        // Rule 2: The Client must exist and belong to this Tenant.
        RuleFor(v => v)
            .MustAsync(ClientBelongsToTenant)
            .When(v => v.TenantId > 0 && v.ClientId > 0)
            .WithName("ClientId")
            .WithMessage(v =>
                $"User {v.ClientId} was not found as a Client in Tenant {v.TenantId}.")
            .WithErrorCode("Assignment.InvalidClient");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private async Task<bool> TenantHasActiveTestAccess(
        AssignTestCommand command,
        CancellationToken cancellationToken)
    {
        return await _context.TenantTestAccesses
            .AsNoTracking()
            .AnyAsync(
                a => a.TenantId == command.TenantId
                  && a.TestId   == command.TestId
                  && a.IsActive,
                cancellationToken);
    }

    private async Task<bool> ClientBelongsToTenant(
        AssignTestCommand command,
        CancellationToken cancellationToken)
    {
        // The global query filter on Users already scopes by TenantId,
        // so we additionally verify the Role is Client.
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Id       == command.ClientId
                  && u.TenantId == command.TenantId
                  && u.Role     == UserRole.Client,
                cancellationToken);
    }
}
