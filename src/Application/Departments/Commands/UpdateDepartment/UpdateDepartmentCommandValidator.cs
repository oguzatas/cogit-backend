using backend.Application.Common.Interfaces;

namespace backend.Application.Departments.Commands.UpdateDepartment;

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDepartmentCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Id)
            .GreaterThan(0);

        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueWithinTenant)
                .WithMessage("A Department with this name already exists in this Tenant.")
                .WithErrorCode("Department.DuplicateName");
    }

    // Looks up the existing department to get its TenantId, then checks uniqueness.
    private async Task<bool> BeUniqueWithinTenant(
        UpdateDepartmentCommand cmd, string name, CancellationToken ct)
    {
        var existing = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == cmd.Id)
            .Select(d => new { d.TenantId })
            .FirstOrDefaultAsync(ct);

        if (existing is null) return true; // NotFound will be caught by the handler

        return !await _context.Departments
            .AnyAsync(d => d.TenantId == existing.TenantId && d.Name == name && d.Id != cmd.Id, ct);
    }
}
