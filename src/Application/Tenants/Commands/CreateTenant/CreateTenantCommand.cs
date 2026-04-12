using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.Tenants.Commands.CreateTenant;

public record CreateTenantCommand : IRequest<int>
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateTenantCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var entity = new Tenant
        {
            Name        = request.Name,
            Description = request.Description,
            IsDeleted   = false
        };

        _context.Tenants.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
