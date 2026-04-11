using backend.Application.Common.Interfaces;
using backend.Domain.Entities;

namespace backend.Application.ScoringScales.Commands.CreateScoringScale;

/// <summary>
/// Creates a new ScoringScale for an existing (global) Test.
/// Only System Admins should be permitted to call this.
/// </summary>
public record CreateScoringScaleCommand : IRequest<int>
{
    /// <summary>The global Test this scale belongs to.</summary>
    public int TestId { get; init; }

    /// <summary>Human-readable name, e.g. "Depression Sub-scale".</summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Mathematical expression referencing question VariableKeys in [brackets],
    /// e.g. "[Q1] + [Q2] * 5".
    /// </summary>
    public string FormulaExpression { get; init; } = default!;
}

public class CreateScoringScaleCommandHandler : IRequestHandler<CreateScoringScaleCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateScoringScaleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateScoringScaleCommand request, CancellationToken cancellationToken)
    {
        // Guard: the Test must exist (global query filter already excludes soft-deleted rows).
        var test = await _context.Tests
            .FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken);

        Guard.Against.NotFound(request.TestId, test);

        var entity = new ScoringScale
        {
            TestId           = request.TestId,
            Name             = request.Name,
            FormulaExpression = request.FormulaExpression,
            IsDeleted        = false
        };

        _context.ScoringScales.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
