using System.Security.Cryptography;
using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.InviteCodes.Commands.CreateInviteCode;

public record CreateInviteCodeCommand : IRequest<CreateInviteCodeResult>
{
    public int TenantId { get; init; }
    public int DepartmentId { get; init; }

    /// <summary>
    /// Maximum number of redemptions. NULL = unlimited.
    /// Example: 50 means the first 50 people to redeem the code succeed; further attempts are rejected.
    /// </summary>
    public int? MaxUses { get; init; }

    /// <summary>Optional hard expiry. NULL = code never expires on its own.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

public record CreateInviteCodeResult(
    int Id,
    string Code,
    int? MaxUses,
    DateTimeOffset? ExpiresAt);

public class CreateInviteCodeCommandHandler : IRequestHandler<CreateInviteCodeCommand, CreateInviteCodeResult>
{
    private readonly IApplicationDbContext _context;

    public CreateInviteCodeCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<CreateInviteCodeResult> Handle(
        CreateInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var department = await _context.Departments
            .FirstOrDefaultAsync(
                d => d.Id == request.DepartmentId && d.TenantId == request.TenantId,
                cancellationToken);

        Guard.Against.NotFound(request.DepartmentId, department);

        // 32 random bytes → 64-char hex string (URL-safe, cryptographically strong).
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var entity = new InviteCode
        {
            TenantId     = request.TenantId,
            DepartmentId = request.DepartmentId,
            Code         = code,
            UsageCount   = 0,
            MaxUses      = request.MaxUses,
            ExpiresAt    = request.ExpiresAt,
            IsRevoked    = false,
            IsDeleted    = false
        };

        _context.InviteCodes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateInviteCodeResult(entity.Id, code, entity.MaxUses, entity.ExpiresAt);
    }
}
