using backend.Application.Common.Interfaces;

namespace backend.Application.TestVariables.Commands.DeleteTestVariable;

/// <summary>
/// Soft-deletes a TestVariable.
/// Note: any ScoringScale formulas that reference this variable's Key will silently
/// evaluate to 0 for that term after deletion. Validate formula impact before deleting.
/// </summary>
public record DeleteTestVariableCommand(int Id) : IRequest;

public class DeleteTestVariableCommandHandler : IRequestHandler<DeleteTestVariableCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteTestVariableCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteTestVariableCommand request, CancellationToken cancellationToken)
    {
        var variable = await _context.TestVariables
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, variable);

        variable.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
