using backend.Application.Common.Interfaces;

namespace backend.Application.InviteCodes.Commands.RedeemInviteCode;

public class RedeemInviteCodeCommandValidator : AbstractValidator<RedeemInviteCodeCommand>
{
    private readonly IApplicationDbContext _context;

    public RedeemInviteCodeCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(v => v.Code)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(v => v.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        // Code must exist, not be used, and not be expired.
        RuleFor(v => v.Code)
            .MustAsync(BeAValidCode)
            .When(v => !string.IsNullOrWhiteSpace(v.Code))
            .WithMessage("The invite code is invalid, has already been used, or has expired.")
            .WithErrorCode("InviteCode.Invalid");

        // Email + code combination: derive tenantId from code and check uniqueness per-tenant.
        // Simple guard: email must not already be a TenantEmployee in any tenant (global uniqueness
        // is intentionally lenient here — the per-tenant unique index on the DB is the hard guard).
        RuleFor(v => v.Email)
            .MustAsync(BeUniqueEmail)
            .When(v => !string.IsNullOrWhiteSpace(v.Email))
            .WithMessage("This email address is already registered.")
            .WithErrorCode("InviteCode.DuplicateEmail");
    }

    private async Task<bool> BeAValidCode(string code, CancellationToken ct)
        => await _context.InviteCodes
            .IgnoreQueryFilters()
            .AnyAsync(c =>
                c.Code == code &&
                !c.IsDeleted &&
                !c.IsUsed &&
                (c.ExpiresAt == null || c.ExpiresAt > DateTimeOffset.UtcNow),
                ct);

    private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
        => !await _context.TenantEmployees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Email == email, ct);
}
