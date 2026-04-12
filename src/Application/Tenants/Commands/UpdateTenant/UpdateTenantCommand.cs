using backend.Application.Common.Interfaces;

namespace backend.Application.Tenants.Commands.UpdateTenant;

public record UpdateTenantCommand : IRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}

public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateTenantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, tenant);

        tenant.Name        = request.Name;
        tenant.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
