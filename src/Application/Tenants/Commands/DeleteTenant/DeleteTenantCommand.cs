using backend.Application.Common.Interfaces;

namespace backend.Application.Tenants.Commands.DeleteTenant;

/// <summary>
/// Soft-deletes a Tenant by setting IsDeleted = true.
/// The row is retained in the database; global query filters hide it from all future queries.
/// </summary>
public record DeleteTenantCommand(int Id) : IRequest;

public class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteTenantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, tenant);

        tenant.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
