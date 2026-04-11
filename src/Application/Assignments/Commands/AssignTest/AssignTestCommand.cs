using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.AssignTest;

/// <summary>
/// Assigns a global Test to a Client within a Tenant.
///
/// Security contract:
///   <see cref="TenantId"/> and <see cref="AssignedByStaffId"/> MUST be set by the
///   endpoint from verified JWT claims — they are never accepted from the request body.
/// </summary>
public record AssignTestCommand : IRequest<int>
{
    /// <summary>Injected from the <c>tenant_id</c> JWT claim by the endpoint.</summary>
    public int TenantId { get; init; }

    /// <summary>Injected from the <c>domain_user_id</c> JWT claim by the endpoint.</summary>
    public int AssignedByStaffId { get; init; }

    /// <summary>The global Test to assign — from request body.</summary>
    public int TestId { get; init; }

    /// <summary>The domain User (Client role) who will take the test — from request body.</summary>
    public int ClientId { get; init; }
}

public class AssignTestCommandHandler : IRequestHandler<AssignTestCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AssignTestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(AssignTestCommand request, CancellationToken cancellationToken)
    {
        // Guard: the Tenant must have active access to this Test.
        // (Validator already checks this, but we guard here for defence-in-depth.)
        var access = await _context.TenantTestAccesses
            .FirstOrDefaultAsync(
                a => a.TenantId == request.TenantId
                  && a.TestId   == request.TestId
                  && a.IsActive,
                cancellationToken);

        Guard.Against.NotFound(request.TestId, access);

        // Guard: the Client must exist (global query filter already scopes to TenantId).
        var client = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.ClientId, cancellationToken);

        Guard.Against.NotFound(request.ClientId, client);

        var entity = new Assignment
        {
            TenantId          = request.TenantId,
            TestId            = request.TestId,
            ClientId          = request.ClientId,
            AssignedByStaffId = request.AssignedByStaffId,
            Status            = AssignmentStatus.Pending,
            IsDeleted         = false
        };

        _context.Assignments.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
