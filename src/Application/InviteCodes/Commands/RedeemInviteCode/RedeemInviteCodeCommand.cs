using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.InviteCodes.Commands.RedeemInviteCode;

/// <summary>
/// Flow B — Self-Service Invite.
/// Public endpoint: the invitee supplies the code + their name and email.
/// Creates a TenantEmployee under the Department encoded in the invite code.
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
        // IgnoreQueryFilters: this handler is called by an anonymous user who has no
        // TenantId claim, so the tenant-scoped global query filter would block the lookup.
        var invite = await _context.InviteCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Code == request.Code && !c.IsDeleted, cancellationToken);

        Guard.Against.NotFound(request.Code, invite);

        // Validate the code is still usable.
        if (invite.IsUsed)
            throw new InvalidOperationException("This invite code has already been used.");

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("This invite code has expired.");

        var employee = new TenantEmployee
        {
            TenantId     = invite.TenantId,
            DepartmentId = invite.DepartmentId,
            FullName     = request.FullName,
            Email        = request.Email,
            IsDeleted    = false
        };

        invite.IsUsed = true;

        _context.TenantEmployees.Add(employee);
        await _context.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
