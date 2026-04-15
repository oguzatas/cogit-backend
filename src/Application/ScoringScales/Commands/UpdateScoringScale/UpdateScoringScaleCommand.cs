using backend.Application.Common.Interfaces;

namespace backend.Application.ScoringScales.Commands.UpdateScoringScale;

public record UpdateScoringScaleCommand : IRequest
{
    public int    Id                { get; init; }
    public string Name              { get; init; } = default!;
    public string FormulaExpression { get; init; } = default!;
}

public class UpdateScoringScaleCommandValidator : AbstractValidator<UpdateScoringScaleCommand>
{
    private readonly IFormulaValidationService _formulaValidator;

    public UpdateScoringScaleCommandValidator(
        IApplicationDbContext    context,
        IFormulaValidationService formulaValidator)
    {
        _formulaValidator = formulaValidator;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(c => c.FormulaExpression)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(expr =>
            {
                _formulaValidator.TryEvaluate(expr, out _);
                return _formulaValidator.TryEvaluate(expr, out _);
            })
            .WithMessage("FormulaExpression is not a valid mathematical expression.");
    }
}

public class UpdateScoringScaleCommandHandler : IRequestHandler<UpdateScoringScaleCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateScoringScaleCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateScoringScaleCommand request, CancellationToken cancellationToken)
    {
        var scale = await _context.ScoringScales
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, scale);

        scale.Name              = request.Name;
        scale.FormulaExpression = request.FormulaExpression;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
