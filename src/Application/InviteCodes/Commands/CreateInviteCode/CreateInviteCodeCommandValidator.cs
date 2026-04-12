using backend.Application.Common.Interfaces;

namespace backend.Application.InviteCodes.Commands.CreateInviteCode;

public class CreateInviteCodeCommandValidator : AbstractValidator<CreateInviteCodeCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateInviteCodeCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.TenantId)
            .GreaterThan(0);

        RuleFor(v => v.DepartmentId)
            .GreaterThan(0);

        RuleFor(v => v.MaxUses)
            .GreaterThan(0)
            .When(v => v.MaxUses.HasValue)
            .WithMessage("MaxUses must be at least 1 when specified.");

        RuleFor(v => v.ExpiresAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(v => v.ExpiresAt.HasValue)
            .WithMessage("ExpiresAt must be a future date/time.");

        RuleFor(v => v)
            .MustAsync(DepartmentBelongsToTenant)
            .When(v => v.TenantId > 0 && v.DepartmentId > 0)
            .WithName("DepartmentId")
            .WithMessage(v => $"Department {v.DepartmentId} does not belong to Tenant {v.TenantId}.")
            .WithErrorCode("InviteCode.InvalidDepartment");
    }

    private async Task<bool> DepartmentBelongsToTenant(
        CreateInviteCodeCommand cmd, CancellationToken ct)
        => await _context.Departments
            .AnyAsync(d => d.Id == cmd.DepartmentId && d.TenantId == cmd.TenantId, ct);
}
