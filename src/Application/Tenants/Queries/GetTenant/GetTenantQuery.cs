using backend.Application.Common.Interfaces;
using backend.Application.Tenants.Queries.GetTenants;

namespace backend.Application.Tenants.Queries.GetTenant;

public record GetTenantQuery(int Id) : IRequest<TenantDto>;

public class GetTenantQueryHandler : IRequestHandler<GetTenantQuery, TenantDto>
{
    private readonly IApplicationDbContext _context;

    public GetTenantQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<TenantDto> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new TenantDto(t.Id, t.Name, t.Description, t.Created))
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, tenant);

        return tenant;
    }
}
