using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.InviteCodes.Commands.RedeemInviteCode;

/// <summary>
/// Flow B — Self-Service Invite.
/// Public endpoint: the invitee supplies the code, their name, and email.
/// Creates a TenantEmployee under the Department encoded in the invite code,
/// then increments UsageCount. Rejects if the code is revoked, expired, or exhausted.
/// </summary>
public record RedeemInviteCodeCommand : IRequest<int>
{
    public string Code { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
}

public class RedeemInviteCodeCommandHandler : IRequestHandler<RedeemInviteCodeCommand, int>
{
    private readonly IApplicationDbContext _context;

    public RedeemInviteCodeCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(RedeemInviteCodeCommand request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: anonymous caller has no TenantId claim.
        var invite = await _context.InviteCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Code == request.Code && !c.IsDeleted, cancellationToken);

        Guard.Against.NotFound(request.Code, invite);

        if (invite.IsRevoked)
            throw new InvalidOperationException("This invite code has been revoked.");

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("This invite code has expired.");

        if (invite.MaxUses.HasValue && invite.UsageCount >= invite.MaxUses.Value)
            throw new InvalidOperationException("This invite code has reached its maximum number of uses.");

        var employee = new TenantEmployee
        {
            TenantId     = invite.TenantId,
            DepartmentId = invite.DepartmentId,
            FullName     = request.FullName,
            Email        = request.Email,
            IsDeleted    = false
        };

        invite.UsageCount++;

        _context.TenantEmployees.Add(employee);
        await _context.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
