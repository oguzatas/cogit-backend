using backend.Application.Common.Interfaces;

namespace backend.Application.Tenants.Commands.CreateTenant;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateTenantCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueName)
                .WithMessage("A Tenant with this name already exists.")
                .WithErrorCode("Tenant.DuplicateName");

        RuleFor(v => v.Description)
            .MaximumLength(1000)
            .When(v => v.Description is not null);
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken ct)
        => !await _context.Tenants.AnyAsync(t => t.Name == name, ct);
}
