using backend.Application.Common.Interfaces;

namespace backend.Application.Departments.Commands.DeleteDepartment;

/// <summary>
/// Soft-deletes a Department by setting IsDeleted = true.
/// The row is retained in the database; global query filters hide it from all future queries.
/// </summary>
public record DeleteDepartmentCommand(int Id) : IRequest;

public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteDepartmentCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, dept);

        dept.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
