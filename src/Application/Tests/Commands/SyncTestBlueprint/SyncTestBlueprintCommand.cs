using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;

namespace backend.Application.Tests.Commands.SyncTestBlueprint;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Full blueprint synchronisation: the client sends the entire desired state of
/// a Test's Variables, Metrics (ScoringScales), and Questions (with nested
/// Options → OptionPoints). The handler performs a three-way merge on each
/// collection so the DB converges to exactly what the client sent.
///
/// Semantics (applied independently to every collection and nested collection):
///   • Row with a positive Id (&gt; 0)      → UPDATE the matching DB row in place.
///   • Row with null, 0, or negative Id    → INSERT as new.
///   • Row present in the DB but absent from the payload → hard-DELETE via
///     <c>_context.Remove()</c> so EF Core's cascade propagates to children
///     rather than leaving orphans or nullifying FKs.
///
/// Temporary IDs (atomic bulk insert):
///   A brand-new <see cref="SyncVariableDto"/> may carry a negative
///   <see cref="SyncVariableDto.Id"/> (e.g. <c>-1</c>, <c>-2</c>) that acts as a
///   client-assigned temporary identifier. Any
///   <see cref="SyncOptionPointDto.TestVariableId"/> with the same negative
///   value is rewritten to the real DB-assigned PK after Phase 1 persists the
///   new variables. This allows atomic bulk payloads that create variables and
///   reference them from new OptionPoints in a single request.
///
/// Persistence is a 2-phase save: Phase 1 persists Variables + Metrics so the
/// DB generates real PKs for new variables; Phase 2 remaps any temporary
/// references in the OptionPoints and persists the Questions graph.
///
/// Intended for bulk edits and AI-driven JSON injections that fully replace
/// the structure of a test.
/// </summary>
public record SyncTestBlueprintCommand : IRequest
{
    public int TestId { get; init; }

    public List<SyncVariableDto>  Variables { get; init; } = new();
    public List<SyncMetricDto>    Metrics   { get; init; } = new();
    public List<SyncQuestionDto>  Questions { get; init; } = new();
}

public record SyncVariableDto
{
    /// <summary>
    /// Positive = existing PK (UPDATE).
    /// Null / 0 = new variable (INSERT) with no temp reference.
    /// Negative = new variable (INSERT) carrying a client-assigned temporary ID
    ///          that <see cref="SyncOptionPointDto.TestVariableId"/> can reference
    ///          from elsewhere in the same payload.
    /// </summary>
    public int?   Id           { get; init; }
    public string Name         { get; init; } = default!;
    public string Key          { get; init; } = default!;
    public double DefaultValue { get; init; }
}

public record SyncMetricDto
{
    /// <summary>Existing PK. Null/0 = new metric to insert.</summary>
    public int?   Id                { get; init; }
    public string Name              { get; init; } = default!;
    public string Key               { get; init; } = default!;
    public string FormulaExpression { get; init; } = default!;
    public int    CalculationOrder  { get; init; }
}

public record SyncQuestionDto
{
    /// <summary>Existing PK. Null/0 = new question to insert.</summary>
    public int?              Id           { get; init; }
    public string            Text         { get; init; } = default!;
    public QuestionType      QuestionType { get; init; }
    public int               OrderIndex   { get; init; }
    public string            VariableKey  { get; init; } = default!;
    public QuestionSettings? Settings     { get; init; }

    public List<SyncOptionDto> Options { get; init; } = new();
}

public record SyncOptionDto
{
    /// <summary>Existing PK. Null/0 = new option to insert.</summary>
    public int?     Id           { get; init; }
    public string   Text         { get; init; } = default!;
    public decimal? NumericValue { get; init; }
    public int      OrderIndex   { get; init; }

    public List<SyncOptionPointDto> OptionPoints { get; init; } = new();
}

public record SyncOptionPointDto
{
    /// <summary>Existing PK. Null/0 = new mapping to insert.</summary>
    public int? Id { get; init; }

    /// <summary>
    /// Either the real PK of an existing <see cref="TestVariable"/> on this test
    /// (positive) or a negative temporary ID that matches a new variable's
    /// <see cref="SyncVariableDto.Id"/> in the same payload. Temporary references
    /// are rewritten to real PKs by the handler after Phase 1.
    /// </summary>
    public int    TestVariableId { get; init; }
    public double Points         { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class SyncTestBlueprintCommandValidator : AbstractValidator<SyncTestBlueprintCommand>
{
    private static readonly string IdentifierPattern = @"^[A-Za-z_][A-Za-z0-9_]*$";

    public SyncTestBlueprintCommandValidator()
    {
        RuleFor(c => c.TestId).GreaterThan(0);

        // ── Variables ─────────────────────────────────────────────────────────
        RuleForEach(c => c.Variables).ChildRules(v =>
        {
            v.RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
            v.RuleFor(x => x.Key)
                .NotEmpty()
                .MaximumLength(100)
                .Matches(IdentifierPattern)
                .WithMessage("Variable Key must be a valid identifier.");
        });

        RuleFor(c => c.Variables)
            .Must(vars => vars.Select(v => v.Key).Distinct().Count() == vars.Count)
            .WithMessage("Duplicate variable Keys are not allowed within a single blueprint.");

        // Temporary IDs (negative) must be unique so OptionPoints reference
        // exactly one new variable.
        RuleFor(c => c.Variables)
            .Must(vars =>
            {
                var tempIds = vars.Where(v => v.Id is < 0).Select(v => v.Id!.Value).ToList();
                return tempIds.Distinct().Count() == tempIds.Count;
            })
            .WithMessage("Duplicate temporary variable IDs are not allowed within a single blueprint.");

        // ── Metrics ───────────────────────────────────────────────────────────
        RuleForEach(c => c.Metrics).ChildRules(m =>
        {
            m.RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
            m.RuleFor(x => x.Key)
                .NotEmpty()
                .MaximumLength(100)
                .Matches(IdentifierPattern)
                .WithMessage("Metric Key must be a valid identifier.");
            m.RuleFor(x => x.FormulaExpression).NotEmpty().MaximumLength(2000);
        });

        RuleFor(c => c.Metrics)
            .Must(ms => ms.Select(m => m.Key).Distinct().Count() == ms.Count)
            .WithMessage("Duplicate metric Keys are not allowed within a single blueprint.");

        // ── Questions ─────────────────────────────────────────────────────────
        RuleForEach(c => c.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
            q.RuleFor(x => x.QuestionType).IsInEnum();
            q.RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
            q.RuleFor(x => x.VariableKey)
                .NotEmpty()
                .MaximumLength(100)
                .Matches(IdentifierPattern)
                .WithMessage("VariableKey must be a valid identifier.");

            q.RuleForEach(x => x.Options).ChildRules(opt =>
            {
                opt.RuleFor(o => o.Text).NotEmpty().MaximumLength(1000);
                opt.RuleFor(o => o.OrderIndex).GreaterThanOrEqualTo(0);

                opt.RuleFor(o => o.OptionPoints)
                    .Must(pts => pts.Select(p => p.TestVariableId).Distinct().Count() == pts.Count)
                    .WithMessage("Each option can only award points to each variable once.");

                opt.RuleForEach(o => o.OptionPoints).ChildRules(pt =>
                {
                    // Zero is invalid; positive = real PK, negative = temp ref.
                    pt.RuleFor(p => p.TestVariableId)
                        .NotEqual(0)
                        .WithMessage(
                            "TestVariableId must reference either an existing variable "
                            + "(positive PK) or a temporary variable ID (negative) defined "
                            + "in this payload.");
                });
            });
        });

        RuleFor(c => c.Questions)
            .Must(qs => qs.Select(q => q.VariableKey).Distinct().Count() == qs.Count)
            .WithMessage("Duplicate question VariableKeys are not allowed within a single blueprint.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public class SyncTestBlueprintCommandHandler : IRequestHandler<SyncTestBlueprintCommand>
{
    private readonly IApplicationDbContext _context;

    public SyncTestBlueprintCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(SyncTestBlueprintCommand request, CancellationToken cancellationToken)
    {
        // ── Load the Test and its existing blueprint state ───────────────────
        var test = await _context.Tests
            .FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken);

        Guard.Against.NotFound(request.TestId, test);

        var existingVariables = await _context.TestVariables
            .Where(v => v.TestId == request.TestId)
            .ToListAsync(cancellationToken);

        var existingMetrics = await _context.ScoringScales
            .Where(s => s.TestId == request.TestId)
            .ToListAsync(cancellationToken);

        var existingQuestions = await _context.Questions
            .Include(q => q.Options)
                .ThenInclude(o => o.QuestionOptionPoints)
            .Where(q => q.TestId == request.TestId)
            .ToListAsync(cancellationToken);

        // ────────────────────────────────────────────────────────────────────
        // PHASE 1 — Variables (and Metrics)
        //
        // Sync variables first and persist them so EF Core generates real PKs
        // for any new rows. We track new variables that carry negative "temp
        // IDs" supplied by the client so we can map them to their real PKs
        // before Phase 2 rewrites OptionPoint references.
        //
        // Metrics don't have FK dependencies on variables by ID (they reference
        // variables by Key in their formula expressions), so they can safely be
        // persisted in the same phase.
        // ────────────────────────────────────────────────────────────────────

        var tempIdToNewVariable = new Dictionary<int, TestVariable>();

        SyncVariables(
            request.TestId,
            existingVariables,
            request.Variables,
            tempIdToNewVariable);

        SyncMetrics(request.TestId, existingMetrics, request.Metrics);

        // Persist Phase 1 — this is what assigns real PKs to the new TestVariables.
        await _context.SaveChangesAsync(cancellationToken);

        // Build tempId → realId map now that EF has populated the Ids.
        var variableIdMap = tempIdToNewVariable
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id);

        // ────────────────────────────────────────────────────────────────────
        // PHASE 2 — Questions, Options, OptionPoints
        //
        // Rewrite any OptionPoint.TestVariableId that matches a temporary ID to
        // the real PK, then run the 3-way merge.
        // ────────────────────────────────────────────────────────────────────

        var remappedQuestions = RemapOptionPointReferences(request.Questions, variableIdMap);

        // After remapping, every referenced TestVariableId must be a positive
        // PK that belongs to this test. This guards against stale negative temp
        // IDs that didn't match any new variable, and against positive IDs that
        // belong to a different test.
        var validVariableIds = await _context.TestVariables
            .Where(v => v.TestId == request.TestId)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);
        var validVariableIdSet = validVariableIds.ToHashSet();

        foreach (var p in remappedQuestions.SelectMany(q => q.Options).SelectMany(o => o.OptionPoints))
        {
            if (p.TestVariableId <= 0 || !validVariableIdSet.Contains(p.TestVariableId))
            {
                throw new InvalidOperationException(
                    $"OptionPoint references TestVariableId {p.TestVariableId} which does not "
                    + "belong to this test (or was a temporary ID with no matching new variable "
                    + "in the payload).");
            }
        }

        SyncQuestions(request.TestId, existingQuestions, remappedQuestions);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Temp-ID remapping ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a copy of the incoming Questions list with every OptionPoint's
    /// <see cref="SyncOptionPointDto.TestVariableId"/> rewritten to its real PK
    /// if it matches a temporary ID. OptionPoints whose TestVariableId does not
    /// appear in the map are passed through unchanged — they are assumed to
    /// already be real PKs (validated post-remap).
    ///
    /// DTOs are <c>init</c>-only records, so we rebuild the nested graph via
    /// <c>with</c> expressions rather than mutating in place.
    /// </summary>
    private static List<SyncQuestionDto> RemapOptionPointReferences(
        List<SyncQuestionDto> questions,
        IReadOnlyDictionary<int, int> variableIdMap)
    {
        if (variableIdMap.Count == 0) return questions;

        return questions.Select(q => q with
        {
            Options = q.Options.Select(o => o with
            {
                OptionPoints = o.OptionPoints.Select(p =>
                    variableIdMap.TryGetValue(p.TestVariableId, out var realId)
                        ? p with { TestVariableId = realId }
                        : p).ToList()
            }).ToList()
        }).ToList();
    }

    // ── Collection mergers ────────────────────────────────────────────────────

    private void SyncVariables(
        int testId,
        List<TestVariable> existing,
        List<SyncVariableDto> incoming,
        Dictionary<int, TestVariable> tempIdToNewVariable)
    {
        var keep = incoming.Where(v => v.Id is > 0).Select(v => v.Id!.Value).ToHashSet();

        // DELETE
        foreach (var v in existing.ToList())
        {
            if (!keep.Contains(v.Id))
                _context.TestVariables.Remove(v);
        }

        // UPDATE + INSERT
        foreach (var dto in incoming)
        {
            if (dto.Id is > 0)
            {
                var v = existing.FirstOrDefault(x => x.Id == dto.Id.Value);
                if (v is null) continue;

                v.Name         = dto.Name;
                v.Key          = dto.Key;
                v.DefaultValue = dto.DefaultValue;
            }
            else
            {
                var entity = new TestVariable
                {
                    TestId       = testId,
                    Name         = dto.Name,
                    Key          = dto.Key,
                    DefaultValue = dto.DefaultValue,
                    IsDeleted    = false
                };

                _context.TestVariables.Add(entity);

                // If the client supplied a negative temporary ID, remember the
                // entity reference so we can read its real PK after SaveChanges.
                if (dto.Id is < 0)
                    tempIdToNewVariable[dto.Id.Value] = entity;
            }
        }
    }

    private void SyncMetrics(
        int testId,
        List<ScoringScale> existing,
        List<SyncMetricDto> incoming)
    {
        var keep = incoming.Where(m => m.Id is > 0).Select(m => m.Id!.Value).ToHashSet();

        // DELETE
        foreach (var m in existing.ToList())
        {
            if (!keep.Contains(m.Id))
                _context.ScoringScales.Remove(m);
        }

        // UPDATE + INSERT
        foreach (var dto in incoming)
        {
            if (dto.Id is > 0)
            {
                var m = existing.FirstOrDefault(x => x.Id == dto.Id.Value);
                if (m is null) continue;

                m.Name              = dto.Name;
                m.Key               = dto.Key;
                m.FormulaExpression = dto.FormulaExpression;
                m.CalculationOrder  = dto.CalculationOrder;
            }
            else
            {
                _context.ScoringScales.Add(new ScoringScale
                {
                    TestId            = testId,
                    Name              = dto.Name,
                    Key               = dto.Key,
                    FormulaExpression = dto.FormulaExpression,
                    CalculationOrder  = dto.CalculationOrder,
                    IsDeleted         = false
                });
            }
        }
    }

    private void SyncQuestions(
        int testId,
        List<Question> existing,
        List<SyncQuestionDto> incoming)
    {
        var keep = incoming.Where(q => q.Id is > 0).Select(q => q.Id!.Value).ToHashSet();

        // DELETE — cascades to Options → OptionPoints because both are tracked
        //          and the FK relationships are configured with Cascade.
        foreach (var q in existing.ToList())
        {
            if (!keep.Contains(q.Id))
                _context.Questions.Remove(q);
        }

        // UPDATE + INSERT
        foreach (var dto in incoming)
        {
            if (dto.Id is > 0)
            {
                var q = existing.FirstOrDefault(x => x.Id == dto.Id.Value);
                if (q is null) continue;

                q.Text         = dto.Text;
                q.QuestionType = dto.QuestionType;
                q.OrderIndex   = dto.OrderIndex;
                q.VariableKey  = dto.VariableKey;
                q.Settings     = dto.Settings;

                SyncOptions(q, dto.Options);
            }
            else
            {
                var newQuestion = new Question
                {
                    TestId       = testId,
                    Text         = dto.Text,
                    QuestionType = dto.QuestionType,
                    OrderIndex   = dto.OrderIndex,
                    VariableKey  = dto.VariableKey,
                    Settings     = dto.Settings,
                    IsDeleted    = false
                };

                foreach (var optDto in dto.Options)
                {
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

                    newQuestion.Options.Add(newOption);
                }

                _context.Questions.Add(newQuestion);
            }
        }
    }

    private void SyncOptions(Question question, List<SyncOptionDto> incoming)
    {
        var keep = incoming.Where(o => o.Id is > 0).Select(o => o.Id!.Value).ToHashSet();

        // DELETE — cascades to the option's OptionPoints (loaded via ThenInclude).
        foreach (var existing in question.Options.ToList())
        {
            if (!keep.Contains(existing.Id))
                _context.QuestionOptions.Remove(existing);
        }

        // UPDATE + INSERT
        foreach (var dto in incoming)
        {
            if (dto.Id is > 0)
            {
                var existing = question.Options.FirstOrDefault(o => o.Id == dto.Id.Value);
                if (existing is null) continue;

                existing.Text         = dto.Text;
                existing.NumericValue = dto.NumericValue;
                existing.OrderIndex   = dto.OrderIndex;

                SyncOptionPoints(existing, dto.OptionPoints);
            }
            else
            {
                var newOption = new QuestionOption
                {
                    Text         = dto.Text,
                    NumericValue = dto.NumericValue,
                    OrderIndex   = dto.OrderIndex,
                    IsDeleted    = false
                };

                foreach (var ptDto in dto.OptionPoints)
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

    private void SyncOptionPoints(QuestionOption option, List<SyncOptionPointDto> incoming)
    {
        var keep = incoming.Where(p => p.Id is > 0).Select(p => p.Id!.Value).ToHashSet();

        // DELETE
        foreach (var existing in option.QuestionOptionPoints.ToList())
        {
            if (!keep.Contains(existing.Id))
                _context.QuestionOptionPoints.Remove(existing);
        }

        // UPDATE + INSERT
        foreach (var dto in incoming)
        {
            if (dto.Id is > 0)
            {
                var existing = option.QuestionOptionPoints.FirstOrDefault(p => p.Id == dto.Id.Value);
                if (existing is null) continue;

                existing.TestVariableId = dto.TestVariableId;
                existing.Points         = dto.Points;
            }
            else
            {
                option.QuestionOptionPoints.Add(new QuestionOptionPoint
                {
                    TestVariableId = dto.TestVariableId,
                    Points         = dto.Points,
                    IsDeleted      = false
                });
            }
        }
    }
}
