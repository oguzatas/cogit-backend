using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.TenantEmployees.Commands.AddTenantEmployee;

/// <summary>
/// Flow A — Silent Provisioning.
/// SuperAdmin manually adds a TenantEmployee to a Department.
/// The employee is created immediately; no email is sent.
/// </summary>
public record AddTenantEmployeeCommand : IRequest<int>
{
    public int TenantId { get; init; }
    public int DepartmentId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
}

public class AddTenantEmployeeCommandHandler : IRequestHandler<AddTenantEmployeeCommand, int>
{
    private readonly IApplicationDbContext _context;

    public AddTenantEmployeeCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(AddTenantEmployeeCommand request, CancellationToken cancellationToken)
    {
        // Guard: Department must exist and belong to the stated Tenant.
        var department = await _context.Departments
            .FirstOrDefaultAsync(
                d => d.Id == request.DepartmentId && d.TenantId == request.TenantId,
                cancellationToken);

        Guard.Against.NotFound(request.DepartmentId, department);

        var employee = new TenantEmployee
        {
            TenantId     = request.TenantId,
            DepartmentId = request.DepartmentId,
            FullName     = request.FullName,
            Email        = request.Email,
            IsDeleted    = false
        };

        _context.TenantEmployees.Add(employee);
        await _context.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}
