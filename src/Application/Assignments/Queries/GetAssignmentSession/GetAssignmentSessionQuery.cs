using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;

namespace backend.Application.Assignments.Queries.GetAssignmentSession;

/// <summary>
/// Returns the test-taker's active session.
/// The query resolves the Assignment from the <c>assignment_id</c> claim in the
/// guest JWT — no ID in the request payload.
/// </summary>
public record GetAssignmentSessionQuery : IRequest<AssignmentSessionDto>;

// ── DTOs — scoring data is intentionally excluded to prevent cheating ─────────

public record AssignmentSessionDto(
    int                      AssignmentId,
    int                      TestId,
    string                   TestName,
    string                   Status,
    List<SessionQuestionDto> Questions);

public record SessionQuestionDto(
    int                     Id,
    string                  Text,
    string                  QuestionType,
    int                     OrderIndex,
    bool                    IsRequired,
    QuestionSettings?       Settings,
    List<SessionOptionDto>  Options);

/// <summary>
/// Option DTO deliberately omits <c>NumericValue</c> and <c>OptionPoints</c>
/// so the scoring weights are never sent to the browser.
/// </summary>
public record SessionOptionDto(int Id, string Text, int OrderIndex);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetAssignmentSessionQueryHandler
    : IRequestHandler<GetAssignmentSessionQuery, AssignmentSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetAssignmentSessionQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<AssignmentSessionDto> Handle(
        GetAssignmentSessionQuery request, CancellationToken cancellationToken)
    {
        var assignmentId = _currentUser.AssignmentId
            ?? throw new UnauthorizedAccessException(
                "This endpoint requires a guest token issued via POST /api/Assignments/access.");

        var assignment = await _context.Assignments
            .Include(a => a.Test)
                .ThenInclude(t => t.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.OrderIndex))
                    .ThenInclude(q => q.Options.Where(o => !o.IsDeleted).OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        Guard.Against.NotFound(assignmentId, assignment);

        // Transition Pending → InProgress on first session load.
        if (assignment.Status == AssignmentStatus.Pending)
        {
            assignment.Status = AssignmentStatus.InProgress;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var questions = assignment.Test.Questions
            .Select(q => new SessionQuestionDto(
                q.Id,
                q.Text,
                q.QuestionType.ToString(),
                q.OrderIndex,
                q.Settings?.IsRequired ?? true,
                q.Settings,
                q.Options
                    .Select(o => new SessionOptionDto(o.Id, o.Text, o.OrderIndex))
                    .ToList()))
            .ToList();

        return new AssignmentSessionDto(
            assignment.Id,
            assignment.TestId,
            assignment.Test.Name,
            assignment.Status.ToString(),
            questions);
    }
}
