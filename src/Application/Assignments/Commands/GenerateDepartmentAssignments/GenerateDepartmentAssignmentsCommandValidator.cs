using backend.Application.Common.Interfaces;

namespace backend.Application.Assignments.Commands.GenerateDepartmentAssignments;

public class GenerateDepartmentAssignmentsCommandValidator
    : AbstractValidator<GenerateDepartmentAssignmentsCommand>
{
    private readonly IApplicationDbContext _context;

    public GenerateDepartmentAssignmentsCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.TenantId).GreaterThan(0);
        RuleFor(v => v.DepartmentId).GreaterThan(0);
        RuleFor(v => v.TestId).GreaterThan(0);

        // Department must belong to the Tenant.
        RuleFor(v => v)
            .MustAsync(DepartmentBelongsToTenant)
            .When(v => v.TenantId > 0 && v.DepartmentId > 0)
            .WithName("DepartmentId")
            .WithMessage(v => $"Department {v.DepartmentId} does not belong to Tenant {v.TenantId}.")
            .WithErrorCode("Generate.InvalidDepartment");

        // Tenant must have active access to the Test.
        RuleFor(v => v)
            .MustAsync(TenantHasActiveTestAccess)
            .When(v => v.TenantId > 0 && v.TestId > 0)
            .WithName("TestId")
            .WithMessage(v => $"Tenant {v.TenantId} does not have active access to Test {v.TestId}.")
            .WithErrorCode("Generate.NoTestAccess");
    }

    private async Task<bool> DepartmentBelongsToTenant(
        GenerateDepartmentAssignmentsCommand cmd, CancellationToken ct)
        => await _context.Departments
            .AnyAsync(d => d.Id == cmd.DepartmentId && d.TenantId == cmd.TenantId, ct);

    private async Task<bool> TenantHasActiveTestAccess(
        GenerateDepartmentAssignmentsCommand cmd, CancellationToken ct)
        => await _context.TenantTestAccesses
            .AnyAsync(a => a.TenantId == cmd.TenantId && a.TestId == cmd.TestId && a.IsActive, ct);
}
