using backend.Application.Common.Interfaces;

namespace backend.Application.Departments.Commands.CreateDepartment;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateDepartmentCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.TenantId)
            .GreaterThan(0);

        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueWithinTenant)
                .WithMessage(v => $"A Department named '{v.Name}' already exists in Tenant {v.TenantId}.")
                .WithErrorCode("Department.DuplicateName");
    }

    private async Task<bool> BeUniqueWithinTenant(
        CreateDepartmentCommand cmd, string name, CancellationToken ct)
        => !await _context.Departments
            .AnyAsync(d => d.TenantId == cmd.TenantId && d.Name == name, ct);
}
