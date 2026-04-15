using backend.Application.Assignments.Commands.GenerateDepartmentAssignments;
using backend.Application.Assignments.Commands.IssueGuestToken;
using backend.Application.Assignments.Commands.ManualGradeAssignment;
using backend.Application.Assignments.Commands.SubmitAssignment;
using backend.Application.Assignments.Commands.UpsertAssignmentAnswer;
using backend.Application.Assignments.Queries.GetAssignmentResults;
using backend.Application.Assignments.Queries.GetAssignmentSession;
using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Assignment management and test-execution flow.
///
/// ── Staff / Admin operations ──────────────────────────────────────────────────
/// POST /api/Assignments                       [TenantStaff|SuperAdmin]
///   Assigns a Test to every TenantEmployee in a Department.
///
/// GET  /api/Assignments/{id}/results          [TenantStaff|SuperAdmin]
///   Returns assignment details, raw answers, manual grades, and calculated results.
///
/// POST /api/Assignments/{id}/manual-grade     [TenantStaff|SuperAdmin]
///   Injects a reviewer's point award for one variable. Triggers NCalc scoring
///   and marks the assignment Completed when all variables have been graded.
///
/// ── Guest (TenantEmployee) operations — require a guest JWT ──────────────────
/// POST /api/Assignments/access                [Anonymous]
///   Exchanges an AccessKey magic-link for a short-lived guest JWT.
///
/// GET  /api/Assignments/session               [GuestJwt]
///   Returns the test session (questions + options, no scoring weights).
///   Transitions status Pending → InProgress on first call.
///
/// PUT  /api/Assignments/answers               [GuestJwt]
///   Upserts a single answer (idempotent — safe to call on every question change).
///
/// POST /api/Assignments/submit                [GuestJwt]
///   Finalises the assignment. Sets status to Completed or AwaitingManualGrading.
/// </summary>
public class Assignments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // ── Staff / Admin ─────────────────────────────────────────────────────
        groupBuilder.MapPost(AssignTestToDepartment)
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.TenantStaff), nameof(UserRole.SuperAdmin)));

        groupBuilder.MapGet(GetAssignmentResults, "{id}/results")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.TenantStaff), nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPost(ManualGrade, "{id}/manual-grade")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.TenantStaff), nameof(UserRole.SuperAdmin)));

        // ── Guest flow (anonymous magic-link → guest JWT) ─────────────────────
        groupBuilder.MapPost(IssueGuestToken, "access")
            .AllowAnonymous();

        // ── Execution (guest JWT required — valid JWT with assignment_id claim) ─
        groupBuilder.MapGet(GetAssignmentSession, "session")
            .RequireAuthorization();

        groupBuilder.MapPut(UpsertAnswer, "answers")
            .RequireAuthorization();

        groupBuilder.MapPost(SubmitAssignment, "submit")
            .RequireAuthorization();
    }

    // ── POST /api/Assignments ─────────────────────────────────────────────────

    [EndpointSummary("Assign a Test to a Department")]
    [EndpointDescription(
        "Creates one Assignment (with a unique AccessKey magic-link) for every TenantEmployee " +
        "in the specified Department. Employees who already have an Assignment for this Test " +
        "are skipped. Returns the count of created and skipped assignments. " +
        "TenantStaff: TenantId is taken from the JWT claim — omit it from the body. " +
        "SuperAdmin: TenantId must be provided in the request body.")]
    public static async Task<Results<Ok<GenerateDepartmentAssignmentsResult>, ForbidHttpResult>>
        AssignTestToDepartment(
            ISender             sender,
            ICurrentUserService currentUserService,
            AssignTestToDepartmentRequest request)
    {
        var tenantId = currentUserService.TenantId ?? request.TenantId;
        if (tenantId is null) return TypedResults.Forbid();

        var result = await sender.Send(new GenerateDepartmentAssignmentsCommand
        {
            TenantId          = tenantId.Value,
            DepartmentId      = request.DepartmentId,
            TestId            = request.TestId,
            AssignedByStaffId = currentUserService.DomainUserId
        });

        return TypedResults.Ok(result);
    }

    // ── GET /api/Assignments/{id}/results ────────────────────────────────────

    [EndpointSummary("Get Assignment Results")]
    [EndpointDescription(
        "Returns the full result view for an assignment: " +
        "details, all raw answers with question text, any manual grades entered, " +
        "and the calculated NCalc scores for each ScoringScale. " +
        "Available to TenantStaff (scoped to their tenant via global query filter) and SuperAdmin.")]
    public static async Task<Ok<AssignmentResultsDto>> GetAssignmentResults(ISender sender, int id)
        => TypedResults.Ok(await sender.Send(new GetAssignmentResultsQuery(id)));

    // ── POST /api/Assignments/{id}/manual-grade ───────────────────────────────

    [EndpointSummary("Submit Manual Grade")]
    [EndpointDescription(
        "Records a reviewer's point award for the specified VariableKey. " +
        "When ALL TestVariables for the test have been graded the scoring engine runs " +
        "automatically and the assignment status transitions to Completed. " +
        "Calling this endpoint again for the same variable replaces the previous grade " +
        "and re-checks completion. " +
        "Returns IsNowCompleted and the current Status.")]
    public static async Task<Ok<ManualGradeResult>> ManualGrade(
        ISender sender, int id, ManualGradeRequest request)
    {
        var result = await sender.Send(new ManualGradeAssignmentCommand
        {
            AssignmentId = id,
            VariableKey  = request.VariableKey,
            Points       = request.Points
        });
        return TypedResults.Ok(result);
    }

    // ── POST /api/Assignments/access ──────────────────────────────────────────

    [EndpointSummary("Issue Guest Token")]
    [EndpointDescription(
        "Exchanges the magic-link AccessKey for a short-lived guest JWT (90 min). " +
        "The returned token carries assignment_id + tenant_id claims and can only be used " +
        "against the session, answers, and submit endpoints. " +
        "Returns 401 if the key is invalid or the assignment is already submitted.")]
    public static async Task<Results<Ok<GuestTokenResponse>, UnauthorizedHttpResult>> IssueGuestToken(
        ISender sender, IssueGuestTokenRequest request)
    {
        try
        {
            var result = await sender.Send(new IssueGuestTokenCommand(request.AccessKey));
            return TypedResults.Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Unauthorized();
        }
    }

    // ── GET /api/Assignments/session ──────────────────────────────────────────

    [EndpointSummary("Get Assignment Session")]
    [EndpointDescription(
        "Returns the assignment details plus all questions and options for the test-taker. " +
        "Scoring weights (NumericValue, OptionPoints) are intentionally omitted. " +
        "If the assignment status was Pending it is automatically set to InProgress.")]
    public static async Task<Ok<AssignmentSessionDto>> GetAssignmentSession(ISender sender)
        => TypedResults.Ok(await sender.Send(new GetAssignmentSessionQuery()));

    // ── PUT /api/Assignments/answers ──────────────────────────────────────────

    [EndpointSummary("Upsert an Answer")]
    [EndpointDescription(
        "Creates or updates the answer for a specific question within the current assignment. " +
        "Safe to call repeatedly as the test-taker changes their selections. " +
        "Returns 400 if the assignment is already completed/submitted.")]
    public static async Task<NoContent> UpsertAnswer(
        ISender sender, UpsertAssignmentAnswerCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // ── POST /api/Assignments/submit ──────────────────────────────────────────

    [EndpointSummary("Submit Assignment")]
    [EndpointDescription(
        "Finalises the assignment. " +
        "Sets status to Completed (all auto-gradable) or AwaitingManualGrading (has TextInput questions). " +
        "Returns the final status in the response body.")]
    public static async Task<Ok<SubmitAssignmentResult>> SubmitAssignment(ISender sender)
        => TypedResults.Ok(await sender.Send(new SubmitAssignmentCommand()));
}

// ── Request records ───────────────────────────────────────────────────────────

public record AssignTestToDepartmentRequest
{
    public int? TenantId     { get; init; }
    public int  DepartmentId { get; init; }
    public int  TestId       { get; init; }
}

public record IssueGuestTokenRequest(string AccessKey);
public record ManualGradeRequest(string VariableKey, double Points);
