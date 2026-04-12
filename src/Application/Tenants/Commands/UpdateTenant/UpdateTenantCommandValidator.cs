using backend.Application.Common.Interfaces;

namespace backend.Application.Tenants.Commands.UpdateTenant;

public class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateTenantCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Id)
            .GreaterThan(0);

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

    private async Task<bool> BeUniqueName(UpdateTenantCommand cmd, string name, CancellationToken ct)
        => !await _context.Tenants.AnyAsync(t => t.Name == name && t.Id != cmd.Id, ct);
}
