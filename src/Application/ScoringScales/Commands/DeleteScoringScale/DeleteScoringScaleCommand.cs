using backend.Application.Common.Interfaces;

namespace backend.Application.ScoringScales.Commands.DeleteScoringScale;

/// <summary>
/// Soft-deletes a ScoringScale (metric).
/// Existing AssignmentResult rows that reference this scale are retained in the database.
/// </summary>
public record DeleteScoringScaleCommand(int Id) : IRequest;

public class DeleteScoringScaleCommandHandler : IRequestHandler<DeleteScoringScaleCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteScoringScaleCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(DeleteScoringScaleCommand request, CancellationToken cancellationToken)
    {
        var scale = await _context.ScoringScales
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, scale);

        scale.IsDeleted = true;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
