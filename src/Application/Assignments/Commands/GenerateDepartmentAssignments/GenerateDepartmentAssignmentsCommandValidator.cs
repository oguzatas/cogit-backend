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

        // Test must exist and be published.
        // Access is granted at the Assignment level via a single-use AccessKey magic link —
        // there is no global tenant-level test access model.
        RuleFor(v => v.TestId)
            .MustAsync(TestExistsAndIsPublished)
            .When(v => v.TestId > 0)
            .WithMessage(v => $"Test {v.TestId} does not exist or is not yet published.")
            .WithErrorCode("Generate.TestNotPublished");
    }

    private async Task<bool> DepartmentBelongsToTenant(
        GenerateDepartmentAssignmentsCommand cmd, CancellationToken ct)
        => await _context.Departments
            .AnyAsync(d => d.Id == cmd.DepartmentId && d.TenantId == cmd.TenantId, ct);

    private async Task<bool> TestExistsAndIsPublished(int testId, CancellationToken ct)
        => await _context.Tests
            .AnyAsync(t => t.Id == testId && t.IsPublished, ct);
}
