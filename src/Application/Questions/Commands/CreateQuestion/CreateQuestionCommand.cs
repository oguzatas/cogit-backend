using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;

namespace backend.Application.Questions.Commands.CreateQuestion;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Aggregate root creation: one command saves Question + Options + OptionPoints
/// atomically in a single EF Core unit of work.
/// </summary>
public record CreateQuestionCommand : IRequest<int>
{
    public int              TestId      { get; init; }
    public string           Text        { get; init; } = default!;
    public QuestionType     QuestionType{ get; init; }
    public int              OrderIndex  { get; init; }
    public string           VariableKey { get; init; } = default!;
    public QuestionSettings? Settings   { get; init; }
    public List<CreateOptionDto> Options{ get; init; } = new();
}

public record CreateOptionDto
{
    public string  Text         { get; init; } = default!;
    public decimal? NumericValue { get; init; }
    public int     OrderIndex   { get; init; }
    public List<CreateOptionPointDto> OptionPoints { get; init; } = new();
}

public record CreateOptionPointDto
{
    public int    TestVariableId { get; init; }
    public double Points         { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class CreateQuestionCommandValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionCommandValidator(IApplicationDbContext context)
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
            .WithMessage("VariableKey must be a valid identifier.");

        // Duplicate VariableKey within the same test is not allowed.
        RuleFor(c => c.VariableKey)
            .MustAsync(async (cmd, key, ct) =>
                !await context.Questions.AnyAsync(q => q.TestId == cmd.TestId && q.VariableKey == key, ct))
            .WithMessage("A question with this VariableKey already exists on the test.");

        RuleForEach(c => c.Options).ChildRules(opt =>
        {
            opt.RuleFor(o => o.Text)
               .NotEmpty()
               .MaximumLength(1000);

            opt.RuleFor(o => o.OrderIndex)
               .GreaterThanOrEqualTo(0);

            opt.RuleForEach(o => o.OptionPoints).ChildRules(pt =>
            {
                pt.RuleFor(p => p.TestVariableId)
                  .GreaterThan(0)
                  .WithMessage("TestVariableId must be a positive integer.");
            });

            // No duplicate TestVariableId within a single option.
            opt.RuleFor(o => o.OptionPoints)
               .Must(pts => pts.Select(p => p.TestVariableId).Distinct().Count() == pts.Count)
               .WithMessage("Each option can only award points to each variable once.");
        });

        // All referenced TestVariableIds must belong to the same Test.
        RuleFor(c => c)
            .MustAsync(async (cmd, ct) =>
            {
                var allVariableIds = cmd.Options
                    .SelectMany(o => o.OptionPoints)
                    .Select(p => p.TestVariableId)
                    .Distinct()
                    .ToList();

                if (allVariableIds.Count == 0) return true;

                var validCount = await context.TestVariables
                    .CountAsync(v => v.TestId == cmd.TestId
                                  && allVariableIds.Contains(v.Id), ct);

                return validCount == allVariableIds.Count;
            })
            .WithMessage("One or more TestVariableIds do not belong to this test.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateQuestionCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        CreateQuestionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NotFound(
            request.TestId,
            await _context.Tests.FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken));

        // Build the full aggregate graph; EF Core will INSERT all rows in one round-trip.
        var question = new Question
        {
            TestId       = request.TestId,
            Text         = request.Text,
            QuestionType = request.QuestionType,
            OrderIndex   = request.OrderIndex,
            VariableKey  = request.VariableKey,
            Settings     = request.Settings,
            IsDeleted    = false
        };

        foreach (var optDto in request.Options)
        {
            var option = new QuestionOption
            {
                Text         = optDto.Text,
                NumericValue = optDto.NumericValue,
                OrderIndex   = optDto.OrderIndex,
                IsDeleted    = false
            };

            foreach (var ptDto in optDto.OptionPoints)
            {
                option.QuestionOptionPoints.Add(new QuestionOptionPoint
                {
                    TestVariableId = ptDto.TestVariableId,
                    Points         = ptDto.Points,
                    IsDeleted      = false
                });
            }

            question.Options.Add(option);
        }

        _context.Questions.Add(question);

        // Single SaveChangesAsync — all nested inserts happen in the same transaction.
        await _context.SaveChangesAsync(cancellationToken);

        return question.Id;
    }
}
