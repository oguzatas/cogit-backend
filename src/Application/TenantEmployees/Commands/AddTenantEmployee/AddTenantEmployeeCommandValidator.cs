using backend.Application.Common.Interfaces;

namespace backend.Application.TenantEmployees.Commands.AddTenantEmployee;

public class AddTenantEmployeeCommandValidator : AbstractValidator<AddTenantEmployeeCommand>
{
    private readonly IApplicationDbContext _context;

    public AddTenantEmployeeCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.TenantId)
            .GreaterThan(0);

        RuleFor(v => v.DepartmentId)
            .GreaterThan(0);

        RuleFor(v => v.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        // Department must belong to the given Tenant.
        RuleFor(v => v)
            .MustAsync(DepartmentBelongsToTenant)
            .When(v => v.TenantId > 0 && v.DepartmentId > 0)
            .WithName("DepartmentId")
            .WithMessage(v => $"Department {v.DepartmentId} does not belong to Tenant {v.TenantId}.")
            .WithErrorCode("Employee.InvalidDepartment");

        // Email must be unique within the same Tenant (same email can appear in different tenants).
        RuleFor(v => v)
            .MustAsync(BeUniqueEmailInTenant)
            .When(v => !string.IsNullOrWhiteSpace(v.Email) && v.TenantId > 0)
            .WithName("Email")
            .WithMessage("This email address is already registered in this Tenant.")
            .WithErrorCode("Employee.DuplicateEmail");
    }

    private async Task<bool> DepartmentBelongsToTenant(
        AddTenantEmployeeCommand cmd, CancellationToken ct)
        => await _context.Departments
            .AnyAsync(d => d.Id == cmd.DepartmentId && d.TenantId == cmd.TenantId, ct);

    private async Task<bool> BeUniqueEmailInTenant(
        AddTenantEmployeeCommand cmd, CancellationToken ct)
        => !await _context.TenantEmployees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == cmd.TenantId && e.Email == cmd.Email, ct);
}
