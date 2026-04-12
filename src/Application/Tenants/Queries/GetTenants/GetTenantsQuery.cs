using backend.Application.Common.Interfaces;

namespace backend.Application.Tenants.Queries.GetTenants;

public record GetTenantsQuery : IRequest<List<TenantDto>>;

public record TenantDto(int Id, string Name, string? Description, DateTimeOffset Created);

public class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, List<TenantDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTenantsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<TenantDto>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
        => await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Name, t.Description, t.Created))
            .ToListAsync(cancellationToken);
}
