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

        // Code must exist and still be redeemable (not revoked, not expired, not exhausted).
        RuleFor(v => v.Code)
            .MustAsync(BeRedeemable)
            .When(v => !string.IsNullOrWhiteSpace(v.Code))
            .WithMessage("The invite code is invalid, revoked, expired, or has reached its usage limit.")
            .WithErrorCode("InviteCode.NotRedeemable");

        RuleFor(v => v.Email)
            .MustAsync(BeUniqueEmail)
            .When(v => !string.IsNullOrWhiteSpace(v.Email))
            .WithMessage("This email address is already registered.")
            .WithErrorCode("InviteCode.DuplicateEmail");
    }

    private async Task<bool> BeRedeemable(string code, CancellationToken ct)
    {
        var invite = await _context.InviteCodes
            .IgnoreQueryFilters()
            .Where(c => c.Code == code && !c.IsDeleted)
            .Select(c => new { c.IsRevoked, c.ExpiresAt, c.UsageCount, c.MaxUses })
            .FirstOrDefaultAsync(ct);

        if (invite is null) return false;
        if (invite.IsRevoked) return false;
        if (invite.ExpiresAt.HasValue && invite.ExpiresAt < DateTimeOffset.UtcNow) return false;
        if (invite.MaxUses.HasValue && invite.UsageCount >= invite.MaxUses.Value) return false;

        return true;
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
        => !await _context.TenantEmployees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Email == email, ct);
}
