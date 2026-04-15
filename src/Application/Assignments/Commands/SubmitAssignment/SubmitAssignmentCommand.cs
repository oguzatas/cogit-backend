using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.SubmitAssignment;

/// <summary>
/// Marks an Assignment as Completed or AwaitingManualGrading, then runs the
/// NCalc scoring pipeline when the test requires no human review.
///
/// Decision rule:
///   • TextInput questions present → AwaitingManualGrading (no scoring yet).
///   • No TextInput questions      → Completed + scoring engine executed immediately.
/// </summary>
public record SubmitAssignmentCommand : IRequest<SubmitAssignmentResult>;

public record SubmitAssignmentResult(string FinalStatus);

public class SubmitAssignmentCommandHandler
    : IRequestHandler<SubmitAssignmentCommand, SubmitAssignmentResult>
{
    private readonly IApplicationDbContext  _context;
    private readonly ICurrentUserService    _currentUser;
    private readonly IScoringEngineService  _scoringEngine;

    public SubmitAssignmentCommandHandler(
        IApplicationDbContext  context,
        ICurrentUserService    currentUser,
        IScoringEngineService  scoringEngine)
    {
        _context       = context;
        _currentUser   = currentUser;
        _scoringEngine = scoringEngine;
    }

    public async Task<SubmitAssignmentResult> Handle(
        SubmitAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignmentId = _currentUser.AssignmentId
            ?? throw new UnauthorizedAccessException(
                "This endpoint requires a guest token issued via POST /api/Assignments/access.");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        Guard.Against.NotFound(assignmentId, assignment);

        if (assignment.Status is AssignmentStatus.Completed
                              or AssignmentStatus.AwaitingManualGrading)
            throw new InvalidOperationException("Assignment has already been submitted.");

        if (assignment.Status == AssignmentStatus.Pending)
            throw new InvalidOperationException(
                "Cannot submit an assignment that has not been started. " +
                "Call GET /api/Assignments/session first.");

        var requiresManualGrading = await _context.Questions
            .AnyAsync(q => q.TestId       == assignment.TestId
                        && q.QuestionType == QuestionType.TextInput,
                      cancellationToken);

        if (requiresManualGrading)
        {
            // At least one TextInput question exists — a reviewer must supply manual
            // grades before scoring can run. Persist the status change and return.
            assignment.Status = AssignmentStatus.AwaitingManualGrading;
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // No TextInput questions — score immediately.
            // CalculateResultsAsync sets assignment.Status = Completed internally
            // and calls SaveChangesAsync, so we must NOT call it again here.
            await _scoringEngine.CalculateResultsAsync(assignmentId, cancellationToken);
        }

        return new SubmitAssignmentResult(assignment.Status.ToString());
    }
}
