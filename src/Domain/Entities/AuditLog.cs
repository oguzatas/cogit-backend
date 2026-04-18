namespace backend.Domain.Entities;

/// <summary>
/// Immutable audit record written by the MediatR <c>AuditBehaviour</c> after every
/// successful Command execution.
///
/// Design notes:
///   • Does NOT inherit <see cref="backend.Domain.Common.BaseAuditableEntity"/> to
///     avoid circular logging (writing an audit of an audit write).
///   • No <c>IsDeleted</c> — audit logs are append-only and must never be soft-deleted.
///   • <see cref="TenantId"/> is nullable: global operations performed by SuperAdmin
///     (e.g. CreateTest, SyncTestBlueprint) have no tenant scope.
/// </summary>
public class AuditLog
{
    public int    Id         { get; set; }

    /// <summary>ASP.NET Identity sub claim (GUID string) of the actor.</summary>
    public string? UserId    { get; set; }

    /// <summary>
    /// Full MediatR request type name, e.g. "CreateTestCommand",
    /// "SyncTestBlueprintCommand", "SubmitAssignmentCommand".
    /// </summary>
    public string ActionName { get; set; } = default!;

    /// <summary>
    /// Simplified entity/domain name derived from <see cref="ActionName"/>,
    /// e.g. "Test", "Question", "Assignment".
    /// </summary>
    public string EntityName { get; set; } = default!;

    /// <summary>
    /// PK of the affected entity.
    /// • For creates: the newly generated Id returned by the handler.
    /// • For updates/deletes: the Id from the request.
    /// Null for commands that do not target a single identifiable row
    /// (e.g. bulk operations).
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>UTC timestamp when the command completed successfully.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Tenant context at the time of the action.
    /// Null when the actor is a SuperAdmin with no tenant scope.
    /// </summary>
    public int? TenantId { get; set; }
}
