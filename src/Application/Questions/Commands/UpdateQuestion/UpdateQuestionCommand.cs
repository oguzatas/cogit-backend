using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;

namespace backend.Application.Questions.Commands.UpdateQuestion;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Full aggregate-root update: the client sends the entire desired state of the
/// Question including its Options and each Option's OptionPoints.
/// The handler performs a three-way merge (delete / update / add) for each
/// nested collection so the DB converges to exactly what the client sent.
/// </summary>
public record UpdateQuestionCommand : IRequest
{
    public int               Id           { get; init; }
    public string            Text         { get; init; } = default!;
    public QuestionType      QuestionType { get; init; }
    public int               OrderIndex   { get; init; }
    public string            VariableKey  { get; init; } = default!;
    public QuestionSettings? Settings     { get; init; }

    /// <summary>
    /// Complete desired state of the options collection.
    /// • Options with a non-zero Id → UPDATE in-place.
    /// • Options with no Id (or Id = null/0) → INSERT as new.
    /// • Options present in the DB but absent here → DELETE (hard-delete with cascade).
    /// </summary>
    public List<UpdateOptionDto> Options { get; init; } = new();
}

/// <summary>
/// Represents a single option within the update payload.
/// </summary>
public record UpdateOptionDto
{
    /// <summary>
    /// Existing option PK. Null / 0 = new option to insert.
    /// </summary>
    public int?    Id           { get; init; }
    public string  Text         { get; init; } = default!;
    public decimal? NumericValue { get; init; }
    public int     OrderIndex   { get; init; }

    /// <summary>
    /// Same merge semantics as the parent options collection.
    /// </summary>
    public List<UpdateOptionPointDto> OptionPoints { get; init; } = new();
}

/// <summary>
/// Represents a single variable → points mapping for an option.
/// </summary>
public record UpdateOptionPointDto
{
    /// <summary>
    /// Existing point-mapping PK. Null / 0 = new mapping to insert.
    /// </summary>
    public int?   Id             { get; init; }
    public int    TestVariableId { get; init; }
    public double Points         { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

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
            // Unique within the test, excluding self.
            .MustAsync(async (cmd, key, ct) =>
            {
                var question = await context.Questions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == cmd.Id, ct);

                if (question is null) return true; // NotFound guard fires in handler

                return !await context.Questions
                    .AnyAsync(q => q.TestId      == question.TestId
                               && q.VariableKey  == key
                               && q.Id           != cmd.Id, ct);
            })
            .WithMessage("A question with this VariableKey already exists on the test.");

        // ── Per-option rules ──────────────────────────────────────────────────
        RuleForEach(c => c.Options).ChildRules(opt =>
        {
            opt.RuleFor(o => o.Text)
               .NotEmpty()
               .MaximumLength(1000);

            opt.RuleFor(o => o.OrderIndex)
               .GreaterThanOrEqualTo(0);

            // No duplicate TestVariableId within a single option's points.
            opt.RuleFor(o => o.OptionPoints)
               .Must(pts => pts.Select(p => p.TestVariableId).Distinct().Count() == pts.Count)
               .WithMessage("Each option can only award points to each variable once.");

            opt.RuleForEach(o => o.OptionPoints).ChildRules(pt =>
            {
                pt.RuleFor(p => p.TestVariableId)
                  .GreaterThan(0)
                  .WithMessage("TestVariableId must be a positive integer.");
            });
        });

        // ── All referenced TestVariableIds must belong to the question's test ─
        RuleFor(c => c)
            .MustAsync(async (cmd, ct) =>
            {
                var allVariableIds = cmd.Options
                    .SelectMany(o => o.OptionPoints)
                    .Select(p => p.TestVariableId)
                    .Distinct()
                    .ToList();

                if (allVariableIds.Count == 0) return true;

                // Look up the question's TestId so we can scope the check.
                var question = await context.Questions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == cmd.Id, ct);

                if (question is null) return true; // guard fires in handler

                var validCount = await context.TestVariables
                    .CountAsync(v => v.TestId == question.TestId
                                  && allVariableIds.Contains(v.Id), ct);

                return validCount == allVariableIds.Count;
            })
            .WithMessage("One or more TestVariableIds do not belong to this question's test.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public class UpdateQuestionCommandHandler : IRequestHandler<UpdateQuestionCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateQuestionCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        // ── Load the full aggregate graph with change-tracking enabled ─────────
        //
        // ThenInclude is critical: without it the QuestionOptionPoints collection
        // would be empty and the nested sync would incorrectly add duplicates.
        var question = await _context.Questions
            .Include(q => q.Options)
                .ThenInclude(o => o.QuestionOptionPoints)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, question);

        // ── 1. Update scalar properties ───────────────────────────────────────
        question.Text         = request.Text;
        question.QuestionType = request.QuestionType;
        question.OrderIndex   = request.OrderIndex;
        question.VariableKey  = request.VariableKey;
        question.Settings     = request.Settings;

        // ── 2. Sync Options ───────────────────────────────────────────────────
        SyncOptions(question, request.Options);

        // ── 3. Persist — single unit of work for all nested changes ───────────
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Performs a DELETE / UPDATE / ADD merge of the Options collection.
    /// Hard-deletes removed options via <see cref="IApplicationDbContext.QuestionOptions"/>.Remove()
    /// so EF Core propagates the cascade to the tracked QuestionOptionPoints children
    /// rather than leaving orphaned rows or merely nullifying the FK.
    /// </summary>
    private void SyncOptions(Question question, List<UpdateOptionDto> incomingOptions)
    {
        // Build a set of option IDs the client wants to keep / update.
        var keepOptionIds = incomingOptions
            .Where(o => o.Id is > 0)
            .Select(o => o.Id!.Value)
            .ToHashSet();

        // ── DELETE: options absent from the incoming list ─────────────────────
        //
        // ToList() materialises a snapshot so we are not modifying the tracked
        // collection while iterating over it.
        // Because QuestionOption → QuestionOptionPoints is configured with
        // OnDelete(DeleteBehavior.Cascade) and the points are already loaded in
        // the change tracker (via ThenInclude), EF Core will automatically mark
        // each option's points as Deleted when the option itself is Deleted —
        // no manual nested removal required here.
        foreach (var existing in question.Options.ToList())
        {
            if (!keepOptionIds.Contains(existing.Id))
                _context.QuestionOptions.Remove(existing);
        }

        // ── UPDATE existing + ADD new ─────────────────────────────────────────
        foreach (var optDto in incomingOptions)
        {
            if (optDto.Id is > 0)
            {
                // UPDATE — find the tracked entity (may be absent if ID is stale/invalid;
                // skip gracefully since the validator has already verified ownership).
                var existing = question.Options
                    .FirstOrDefault(o => o.Id == optDto.Id.Value);

                if (existing is null) continue;

                existing.Text         = optDto.Text;
                existing.NumericValue = optDto.NumericValue;
                existing.OrderIndex   = optDto.OrderIndex;

                // Recurse into the nested OptionPoints collection.
                SyncOptionPoints(existing, optDto.OptionPoints);
            }
            else
            {
                // ADD — build the new option with its points graph so EF inserts
                // all rows in one round-trip via the same SaveChangesAsync call.
                var newOption = new QuestionOption
                {
                    Text         = optDto.Text,
                    NumericValue = optDto.NumericValue,
                    OrderIndex   = optDto.OrderIndex,
                    IsDeleted    = false
                };

                foreach (var ptDto in optDto.OptionPoints)
                {
                    newOption.QuestionOptionPoints.Add(new QuestionOptionPoint
                    {
                        TestVariableId = ptDto.TestVariableId,
                        Points         = ptDto.Points,
                        IsDeleted      = false
                    });
                }

                question.Options.Add(newOption);
            }
        }
    }

    /// <summary>
    /// Performs a DELETE / UPDATE / ADD merge of one option's OptionPoints collection.
    /// Uses the same hard-delete pattern as <see cref="SyncOptions"/> for consistency
    /// and to avoid unique-index violations on (OptionId, TestVariableId).
    /// </summary>
    private void SyncOptionPoints(QuestionOption option, List<UpdateOptionPointDto> incomingPoints)
    {
        // Build a set of point-mapping IDs the client wants to keep / update.
        var keepPointIds = incomingPoints
            .Where(p => p.Id is > 0)
            .Select(p => p.Id!.Value)
            .ToHashSet();

        // ── DELETE: point-mappings absent from the incoming list ──────────────
        foreach (var existing in option.QuestionOptionPoints.ToList())
        {
            if (!keepPointIds.Contains(existing.Id))
                _context.QuestionOptionPoints.Remove(existing);
        }

        // ── UPDATE existing + ADD new ─────────────────────────────────────────
        foreach (var ptDto in incomingPoints)
        {
            if (ptDto.Id is > 0)
            {
                // UPDATE
                var existing = option.QuestionOptionPoints
                    .FirstOrDefault(p => p.Id == ptDto.Id.Value);

                if (existing is null) continue;

                // TestVariableId change is allowed (user re-maps the variable).
                existing.TestVariableId = ptDto.TestVariableId;
                existing.Points         = ptDto.Points;
            }
            else
            {
                // ADD
                option.QuestionOptionPoints.Add(new QuestionOptionPoint
                {
                    TestVariableId = ptDto.TestVariableId,
                    Points         = ptDto.Points,
                    IsDeleted      = false
                });
            }
        }
    }
}
