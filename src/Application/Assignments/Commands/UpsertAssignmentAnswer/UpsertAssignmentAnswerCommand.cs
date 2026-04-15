using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.UpsertAssignmentAnswer;

public record UpsertAssignmentAnswerCommand : IRequest
{
    /// <summary>The question being answered.</summary>
    public int         QuestionId        { get; init; }

    /// <summary>Selected option IDs for choice-based questions.</summary>
    public List<int>   SelectedOptionIds { get; init; } = new();

    /// <summary>Numeric response for Rating / LikertScale questions.</summary>
    public double?     NumberValue       { get; init; }

    /// <summary>Open-text response for TextInput questions.</summary>
    public string?     TextValue         { get; init; }
}

public class UpsertAssignmentAnswerCommandValidator
    : AbstractValidator<UpsertAssignmentAnswerCommand>
{
    public UpsertAssignmentAnswerCommandValidator()
    {
        RuleFor(c => c.QuestionId).GreaterThan(0);

        RuleFor(c => c.TextValue)
            .MaximumLength(4000)
            .When(c => c.TextValue is not null);
    }
}

public class UpsertAssignmentAnswerCommandHandler
    : IRequestHandler<UpsertAssignmentAnswerCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpsertAssignmentAnswerCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(
        UpsertAssignmentAnswerCommand request, CancellationToken cancellationToken)
    {
        var assignmentId = _currentUser.AssignmentId
            ?? throw new UnauthorizedAccessException(
                "This endpoint requires a guest token issued via POST /api/Assignments/access.");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        Guard.Against.NotFound(assignmentId, assignment);

        if (assignment.Status is AssignmentStatus.Completed
                              or AssignmentStatus.AwaitingManualGrading)
            throw new InvalidOperationException("Cannot modify answers for a completed assignment.");

        // Validate the question belongs to this assignment's test.
        var questionExists = await _context.Questions
            .AnyAsync(q => q.Id == request.QuestionId
                        && q.TestId == assignment.TestId, cancellationToken);

        if (!questionExists)
            throw new KeyNotFoundException(
                $"Question {request.QuestionId} does not belong to test {assignment.TestId}.");

        // Upsert: find existing answer or create new.
        var existing = await _context.AssignmentAnswers
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId
                                   && a.QuestionId   == request.QuestionId,
                                 cancellationToken);

        if (existing is not null)
        {
            existing.SelectedOptionIds = request.SelectedOptionIds;
            existing.NumberValue       = request.NumberValue;
            existing.TextValue         = request.TextValue;
        }
        else
        {
            _context.AssignmentAnswers.Add(new AssignmentAnswer
            {
                TenantId          = assignment.TenantId,
                AssignmentId      = assignmentId,
                QuestionId        = request.QuestionId,
                SelectedOptionIds = request.SelectedOptionIds,
                NumberValue       = request.NumberValue,
                TextValue         = request.TextValue,
                IsDeleted         = false
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
