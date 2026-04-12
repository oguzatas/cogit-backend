using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.Departments.Commands.CreateDepartment;

public record CreateDepartmentCommand : IRequest<int>
{
    public int TenantId { get; init; }
    public string Name { get; init; } = default!;
}

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateDepartmentCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        Guard.Against.NotFound(request.TenantId, tenant);

        var entity = new Department
        {
            TenantId  = request.TenantId,
            Name      = request.Name,
            IsDeleted = false
        };

        _context.Departments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
