using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;

namespace backend.Application.Questions.Commands.UpdateQuestion;

public record UpdateQuestionCommand : IRequest
{
    public int               Id           { get; init; }
    public string            Text         { get; init; } = default!;
    public QuestionType      QuestionType { get; init; }
    public int               OrderIndex   { get; init; }
    public string            VariableKey  { get; init; } = default!;
    public QuestionSettings? Settings     { get; init; }
}

public class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.Text)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(c => c.QuestionType)
            .IsInEnum();

        RuleFor(c => c.OrderIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(c => c.VariableKey)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[A-Za-z_][A-Za-z0-9_]*$")
            .WithMessage("VariableKey must be a valid identifier.")
            // Unique within test, excluding self.
            .MustAsync(async (cmd, key, ct) =>
            {
                var question = await context.Questions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == cmd.Id, ct);

                if (question is null) return true; // NotFound guard in handler

                return !await context.Questions
                    .AnyAsync(q => q.TestId == question.TestId
                               && q.VariableKey == key
                               && q.Id != cmd.Id, ct);
            })
            .WithMessage("A question with this VariableKey already exists on the test.");
    }
}

public class UpdateQuestionCommandHandler : IRequestHandler<UpdateQuestionCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateQuestionCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _context.Questions
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, question);

        question.Text         = request.Text;
        question.QuestionType = request.QuestionType;
        question.OrderIndex   = request.OrderIndex;
        question.VariableKey  = request.VariableKey;
        question.Settings     = request.Settings;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
