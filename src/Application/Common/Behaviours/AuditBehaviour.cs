using System.Reflection;
using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace backend.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behavior that writes an <see cref="AuditLog"/> record after every
/// successful Command execution.
///
/// Only requests whose type name ends with "Command" are logged; Query requests are
/// silently skipped.  Audit failures are caught and logged as warnings so they never
/// surface as 500 errors to the caller.
///
/// Pipeline position: registered last (innermost wrapper), so it fires immediately
/// after the handler returns — after validation, authorization, and performance
/// measurement have all passed.
/// </summary>
public sealed class AuditBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly bool IsCommand =
        typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal);

    private readonly IApplicationDbContext  _context;
    private readonly ICurrentUserService    _currentUser;
    private readonly ILogger<AuditBehaviour<TRequest, TResponse>> _logger;

    public AuditBehaviour(
        IApplicationDbContext  context,
        ICurrentUserService    currentUser,
        ILogger<AuditBehaviour<TRequest, TResponse>> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Execute the handler first — only log on success.
        var response = await next();

        if (!IsCommand) return response;

        try
        {
            var actionName = typeof(TRequest).Name;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId     = _currentUser.UserId,
                ActionName = actionName,
                EntityName = ExtractEntityName(actionName),
                EntityId   = ExtractEntityId(request, response),
                Timestamp  = DateTimeOffset.UtcNow,
                TenantId   = _currentUser.TenantId
            });

            // Best-effort: a separate SaveChanges so an audit failure never
            // rolls back the primary operation that already succeeded.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AuditBehaviour: failed to write audit log for {CommandName}. " +
                "The primary operation succeeded; audit is best-effort.",
                typeof(TRequest).Name);
        }

        return response;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to determine the primary affected entity's PK.
    ///
    /// Strategy (in order):
    ///   1. If the response is an <c>int</c> it is the newly created row's PK.
    ///   2. Otherwise reflect on well-known Id properties of the request.
    /// </summary>
    private static int? ExtractEntityId(TRequest request, TResponse response)
    {
        // Create commands return the new PK directly.
        if (response is int createdId && createdId > 0)
            return createdId;

        // Update / delete commands carry the PK on the request.
        foreach (var propName in new[] { "Id", "AssignmentId", "TestId", "QuestionId",
                                         "TenantId", "DepartmentId", "ScaleId" })
        {
            var prop = typeof(TRequest).GetProperty(
                propName, BindingFlags.Public | BindingFlags.Instance);

            if (prop is null) continue;

            // int? boxes to int when non-null, so a single int pattern covers both.
            if (prop.GetValue(request) is int i && i > 0) return i;
        }

        return null;
    }

    /// <summary>
    /// Strips action verb prefixes and the "Command" suffix to produce a concise
    /// entity/domain label.
    ///
    /// Examples:
    ///   "CreateTestCommand"                    → "Test"
    ///   "UpdateQuestionCommand"                → "Question"
    ///   "SyncTestBlueprintCommand"             → "TestBlueprint"
    ///   "GenerateDepartmentAssignmentsCommand" → "DepartmentAssignments"
    ///   "SubmitAssignmentCommand"              → "Assignment"
    ///   "UpsertAssignmentAnswerCommand"        → "AssignmentAnswer"
    /// </summary>
    private static string ExtractEntityName(string commandName)
    {
        // Remove "Command" suffix.
        var name = commandName.EndsWith("Command", StringComparison.Ordinal)
            ? commandName[..^"Command".Length]
            : commandName;

        // Remove leading action verb.
        string[] verbs =
        [
            "Create", "Update", "Delete", "Sync", "Generate", "Submit",
            "Upsert", "Revoke", "Redeem", "Issue", "ManualGrade", "Manual",
            "AddTenant", "Add"
        ];

        foreach (var verb in verbs)
        {
            if (name.StartsWith(verb, StringComparison.Ordinal))
            {
                name = name[verb.Length..];
                break;
            }
        }

        return name.Length > 0 ? name : commandName;
    }
}
