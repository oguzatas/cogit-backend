using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.ManualGradeAssignment;

/// <summary>
/// Records a reviewer's point award for one TestVariable on an AwaitingManualGrading assignment.
///
/// Completion logic:
///   • Upserts a ManualGrade row for the specified variable.
///   • If ALL TestVariables for this test now have a ManualGrade, runs the NCalc scoring
///     pipeline and transitions the assignment to Completed.
///   • Subsequent calls for the same variable replace the previous grade; the completion
///     check re-runs each time.
/// </summary>
public record ManualGradeAssignmentCommand : IRequest<ManualGradeResult>
{
    public int    AssignmentId { get; init; }
    public string VariableKey  { get; init; } = default!;
    public double Points       { get; init; }
}

public record ManualGradeResult(bool IsNowCompleted, string Status);

public class ManualGradeAssignmentCommandValidator
    : AbstractValidator<ManualGradeAssignmentCommand>
{
    public ManualGradeAssignmentCommandValidator()
    {
        RuleFor(c => c.AssignmentId).GreaterThan(0);

        RuleFor(c => c.VariableKey)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[A-Za-z_][A-Za-z0-9_]*$")
            .WithMessage("VariableKey must be a valid identifier.");
    }
}

public class ManualGradeAssignmentCommandHandler
    : IRequestHandler<ManualGradeAssignmentCommand, ManualGradeResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IScoringEngineService _scoringEngine;

    public ManualGradeAssignmentCommandHandler(
        IApplicationDbContext context, IScoringEngineService scoringEngine)
    {
        _context       = context;
        _scoringEngine = scoringEngine;
    }

    public async Task<ManualGradeResult> Handle(
        ManualGradeAssignmentCommand request, CancellationToken cancellationToken)
    {
        // ── Load & validate assignment ────────────────────────────────────────
        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        Guard.Against.NotFound(request.AssignmentId, assignment);

        if (assignment.Status != AssignmentStatus.AwaitingManualGrading)
            throw new InvalidOperationException(
                $"Assignment is in '{assignment.Status}' status. " +
                "Manual grading is only allowed when status is AwaitingManualGrading.");

        // ── Resolve VariableKey → TestVariable ────────────────────────────────
        var variable = await _context.TestVariables
            .FirstOrDefaultAsync(v => v.TestId == assignment.TestId
                                   && v.Key    == request.VariableKey,
                                 cancellationToken);

        if (variable is null)
            throw new KeyNotFoundException(
                $"Variable '{request.VariableKey}' does not exist on test {assignment.TestId}.");

        // ── Upsert the ManualGrade (bypass global filter to find soft-deleted) ─
        // Use IgnoreQueryFilters so a previously soft-deleted grade for the same
        // variable is found and restored rather than creating a duplicate.
        var existing = await _context.ManualGrades
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.AssignmentId   == request.AssignmentId
                                   && g.TestVariableId == variable.Id,
                                 cancellationToken);

        if (existing is not null)
        {
            existing.Points    = request.Points;
            existing.IsDeleted = false; // restore if previously deleted
        }
        else
        {
            _context.ManualGrades.Add(new ManualGrade
            {
                AssignmentId   = request.AssignmentId,
                TenantId       = assignment.TenantId,
                TestVariableId = variable.Id,
                Points         = request.Points,
                IsDeleted      = false
            });
        }

        // ── Check completion: all variables graded? ───────────────────────────
        // Save the upserted grade first so the count below sees it.
        await _context.SaveChangesAsync(cancellationToken);

        var totalVariables = await _context.TestVariables
            .CountAsync(v => v.TestId == assignment.TestId, cancellationToken);

        var gradedVariables = await _context.ManualGrades
            .CountAsync(g => g.AssignmentId == request.AssignmentId, cancellationToken);

        bool isNowCompleted = false;

        if (gradedVariables >= totalVariables && totalVariables > 0)
        {
            // All variables have been manually graded — run the scoring engine.
            // CalculateResultsAsync sets assignment.Status = Completed internally
            // and calls SaveChangesAsync, so we must NOT call them again here.
            await _scoringEngine.CalculateResultsAsync(request.AssignmentId, cancellationToken);

            isNowCompleted = true;
        }

        return new ManualGradeResult(isNowCompleted, assignment.Status.ToString());
    }
}
