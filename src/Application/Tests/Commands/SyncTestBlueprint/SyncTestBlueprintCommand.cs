using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;
using backend.Domain.ValueObjects;
using FluentValidation.Results;

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
///   • Row with null or 0 Id                → INSERT as new.
///   • Row present in the DB but absent from the payload → hard-DELETE via
///     <c>_context.Remove()</c> so EF Core's cascade propagates to children
///     rather than leaving orphans or nullifying FKs.
///
/// OptionPoint variable references are key-based:
///   <see cref="SyncOptionPointDto.TestVariableKey"/> points to
///   <see cref="SyncVariableDto.Key"/> in the same payload or an existing
///   variable on the test. This makes bulk payloads deterministic and removes
///   temporary integer ID coupling.
///
/// Persistence is a 2-phase save: Phase 1 persists Variables + Metrics so the
/// DB generates real PKs for new variables; Phase 2 resolves
/// <see cref="SyncOptionPointDto.TestVariableKey"/> to real IDs and persists
/// the Questions graph.
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
    /// Null / 0 = new variable (INSERT).
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
    /// Key of the <see cref="TestVariable"/> on this test. The handler resolves
    /// this key to the real PK after Phase 1 saves Variables.
    /// </summary>
    public string TestVariableKey { get; init; } = default!;
    public double Points          { get; init; }
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

        RuleForEach(c => c.Variables)
            .Must(v => v.Id is null || v.Id >= 0)
            .WithMessage("Variable Id must be positive for updates or 0/null for inserts.");

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
                    .Must(pts => pts.Select(p => p.TestVariableKey).Distinct().Count() == pts.Count)
                    .WithMessage("Each option can only award points to each variable once.");

                opt.RuleForEach(o => o.OptionPoints).ChildRules(pt =>
                {
                    pt.RuleFor(p => p.TestVariableKey)
                        .NotEmpty()
                        .MaximumLength(100)
                        .Matches(IdentifierPattern)
                        .WithMessage("TestVariableKey must be a valid identifier.");
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
        // Persist variables first so EF generates real PKs for new rows. Then
        // build a Key → Id map for resolving OptionPoint references.
        // ────────────────────────────────────────────────────────────────────

        SyncVariables(request.TestId, existingVariables, request.Variables);

        SyncMetrics(request.TestId, existingMetrics, request.Metrics);

        // Persist Phase 1 — this assigns real PKs to the new TestVariables.
        await _context.SaveChangesAsync(cancellationToken);

        var currentVariables = await _context.TestVariables
            .Where(v => v.TestId == request.TestId)
            .ToListAsync(cancellationToken);
        var variableKeyToIdMap = currentVariables.ToDictionary(v => v.Key, v => v.Id);

        // ────────────────────────────────────────────────────────────────────
        // PHASE 2 — Questions, Options, OptionPoints
        //
        // Resolve key-based OptionPoint references to real variable IDs while
        // running the 3-way merge.
        // ────────────────────────────────────────────────────────────────────

        SyncQuestions(request.TestId, existingQuestions, request.Questions, variableKeyToIdMap);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Collection mergers ────────────────────────────────────────────────────

    private void SyncVariables(
        int testId,
        List<TestVariable> existing,
        List<SyncVariableDto> incoming)
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
        List<SyncQuestionDto> incoming,
        IReadOnlyDictionary<string, int> variableKeyToIdMap)
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

                SyncOptions(q, dto.Options, variableKeyToIdMap);
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
                            TestVariableId = ResolveVariableId(ptDto.TestVariableKey, variableKeyToIdMap),
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

    private void SyncOptions(
        Question question,
        List<SyncOptionDto> incoming,
        IReadOnlyDictionary<string, int> variableKeyToIdMap)
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

                SyncOptionPoints(existing, dto.OptionPoints, variableKeyToIdMap);
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
                        TestVariableId = ResolveVariableId(ptDto.TestVariableKey, variableKeyToIdMap),
                        Points         = ptDto.Points,
                        IsDeleted      = false
                    });
                }

                question.Options.Add(newOption);
            }
        }
    }

    private void SyncOptionPoints(
        QuestionOption option,
        List<SyncOptionPointDto> incoming,
        IReadOnlyDictionary<string, int> variableKeyToIdMap)
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

                existing.TestVariableId = ResolveVariableId(dto.TestVariableKey, variableKeyToIdMap);
                existing.Points         = dto.Points;
            }
            else
            {
                option.QuestionOptionPoints.Add(new QuestionOptionPoint
                {
                    TestVariableId = ResolveVariableId(dto.TestVariableKey, variableKeyToIdMap),
                    Points         = dto.Points,
                    IsDeleted      = false
                });
            }
        }
    }

    private static int ResolveVariableId(
        string variableKey,
        IReadOnlyDictionary<string, int> variableKeyToIdMap)
    {
        if (variableKeyToIdMap.TryGetValue(variableKey, out var variableId))
            return variableId;

        throw new ValidationException(new[]
        {
            new ValidationFailure(
                nameof(SyncOptionPointDto.TestVariableKey),
                $"Variable Key '{variableKey}' not found in the test blueprint.")
        });
    }
}
