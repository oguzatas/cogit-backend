using System.Security.Cryptography;
using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.InviteCodes.Commands.CreateInviteCode;

public record CreateInviteCodeCommand : IRequest<CreateInviteCodeResult>
{
    public int TenantId { get; init; }
    public int DepartmentId { get; init; }

    /// <summary>Optional expiry. NULL = code never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

public record CreateInviteCodeResult(int Id, string Code, DateTimeOffset? ExpiresAt);

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
            IsUsed       = false,
            ExpiresAt    = request.ExpiresAt,
            IsDeleted    = false
        };

        _context.InviteCodes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateInviteCodeResult(entity.Id, code, entity.ExpiresAt);
    }
}
