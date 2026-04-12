using backend.Application.Common.Interfaces;

namespace backend.Application.Departments.Commands.UpdateDepartment;

public record UpdateDepartmentCommand : IRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
}

public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDepartmentCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, dept);

        dept.Name = request.Name;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
