using backend.Application.Common.Interfaces;

namespace backend.Application.InviteCodes.Commands.RevokeInviteCode;

/// <summary>
/// Explicitly revokes an invite code so it can no longer be redeemed.
/// The row is retained in the database with IsRevoked = true and RevokedAt stamped.
/// </summary>
public record RevokeInviteCodeCommand(int Id) : IRequest;

public class RevokeInviteCodeCommandHandler : IRequestHandler<RevokeInviteCodeCommand>
{
    private readonly IApplicationDbContext _context;

    public RevokeInviteCodeCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(RevokeInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var invite = await _context.InviteCodes
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, invite);

        if (invite.IsRevoked)
            return; // idempotent — already revoked

        invite.IsRevoked  = true;
        invite.RevokedAt  = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
