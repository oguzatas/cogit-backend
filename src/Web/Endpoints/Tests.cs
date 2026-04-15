using backend.Application.Questions.Commands.CreateQuestion;
using backend.Application.Questions.Queries.GetTestQuestions;
using backend.Application.ScoringScales.Commands.CreateScoringScale;
using backend.Application.TestVariables.Commands.CreateTestVariable;
using backend.Application.TestVariables.Queries.GetTestVariables;
using backend.Application.Tests.Commands.CreateTest;
using backend.Application.Tests.Commands.DeleteTest;
using backend.Application.Tests.Commands.UpdateTest;
using backend.Application.Tests.Queries.GetTest;
using backend.Application.Tests.Queries.GetTestMetrics;
using backend.Application.Tests.Queries.GetTests;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Test management (global — no tenant scope).
///
/// ── CRUD ──────────────────────────────────────────────────────────────────────
/// GET    /api/Tests               [SuperAdmin]               — list all tests
/// GET    /api/Tests/{id}          [SuperAdmin]               — single test
/// POST   /api/Tests               [SuperAdmin]               — create test
/// PUT    /api/Tests/{id}          [SuperAdmin]               — update test
/// DELETE /api/Tests/{id}          [SuperAdmin]               — soft-delete test
///
/// ── Blueprint sub-resources ───────────────────────────────────────────────────
/// GET    /api/Tests/{id}/variables   [SuperAdmin|TenantStaff] — list variables
/// POST   /api/Tests/{id}/variables   [SuperAdmin|TenantStaff] — create variable
/// GET    /api/Tests/{id}/metrics     [SuperAdmin|TenantStaff] — list scoring scales
/// POST   /api/Tests/{id}/metrics     [SuperAdmin|TenantStaff] — create scoring scale
/// GET    /api/Tests/{id}/questions   [SuperAdmin|TenantStaff] — list questions (with options+points)
/// POST   /api/Tests/{id}/questions   [SuperAdmin|TenantStaff] — create question (aggregate root)
/// </summary>
public class Tests : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // ── CRUD (SuperAdmin only) ─────────────────────────────────────────────
        groupBuilder.MapGet(GetTests)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapGet(GetTest, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPost(CreateTest)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPut(UpdateTest, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapDelete(DeleteTest, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        // ── Variables sub-resource ────────────────────────────────────────────
        groupBuilder.MapGet(GetTestVariables, "{testId}/variables")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapPost(CreateTestVariable, "{testId}/variables")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        // ── Metrics (ScoringScales) sub-resource ──────────────────────────────
        groupBuilder.MapGet(GetTestMetrics, "{testId}/metrics")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapPost(CreateTestMetric, "{testId}/metrics")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        // ── Questions sub-resource ────────────────────────────────────────────
        groupBuilder.MapGet(GetTestQuestions, "{testId}/questions")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapPost(CreateQuestion, "{testId}/questions")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [EndpointSummary("List all Tests")]
    [EndpointDescription("Returns all non-deleted tests ordered by name.")]
    public static async Task<Ok<List<TestDto>>> GetTests(ISender sender)
        => TypedResults.Ok(await sender.Send(new GetTestsQuery()));

    [EndpointSummary("Get a Test")]
    [EndpointDescription("Returns a single test by id.")]
    public static async Task<Ok<TestDto>> GetTest(ISender sender, int id)
        => TypedResults.Ok(await sender.Send(new GetTestQuery(id)));

    [EndpointSummary("Create a Test")]
    [EndpointDescription("Creates a new test. Name must be unique. IsPublished defaults to false.")]
    public static async Task<Created<int>> CreateTest(ISender sender, CreateTestCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Tests/{id}", id);
    }

    [EndpointSummary("Update a Test")]
    [EndpointDescription("Updates name, description, and isPublished for the specified test.")]
    public static async Task<NoContent> UpdateTest(ISender sender, int id, UpdateTestCommand command)
    {
        await sender.Send(command with { Id = id });
        return TypedResults.NoContent();
    }

    [EndpointSummary("Delete a Test")]
    [EndpointDescription("Soft-deletes the specified test. The record is retained in the database.")]
    public static async Task<NoContent> DeleteTest(ISender sender, int id)
    {
        await sender.Send(new DeleteTestCommand(id));
        return TypedResults.NoContent();
    }

    // ── Variables ─────────────────────────────────────────────────────────────

    [EndpointSummary("List TestVariables")]
    [EndpointDescription("Returns all NCalc scoring variables defined for this test, ordered by key.")]
    public static async Task<Ok<List<TestVariableDto>>> GetTestVariables(ISender sender, int testId)
        => TypedResults.Ok(await sender.Send(new GetTestVariablesQuery(testId)));

    [EndpointSummary("Create a TestVariable")]
    [EndpointDescription(
        "Registers a new scoring variable for this test. " +
        "Key must be unique per test and must be a valid identifier (used in NCalc formulas).")]
    public static async Task<Created<TestVariableDto>> CreateTestVariable(
        ISender sender, int testId, CreateTestVariableCommand command)
    {
        var dto = await sender.Send(command with { TestId = testId });
        return TypedResults.Created($"/api/Tests/{testId}/variables/{dto.Id}", dto);
    }

    // ── Metrics (ScoringScales) ───────────────────────────────────────────────

    [EndpointSummary("List Scoring Metrics")]
    [EndpointDescription("Returns all ScoringScales (NCalc formulas) defined for this test.")]
    public static async Task<Ok<List<ScoringScaleDto>>> GetTestMetrics(ISender sender, int testId)
        => TypedResults.Ok(await sender.Send(new GetTestMetricsQuery(testId)));

    [EndpointSummary("Create a Scoring Metric")]
    [EndpointDescription(
        "Creates a new ScoringScale formula for this test. " +
        "FormulaExpression may reference TestVariable keys as NCalc identifiers (e.g. 'LDR + EMP * 0.5').")]
    public static async Task<Created<int>> CreateTestMetric(
        ISender sender, int testId, CreateScoringScaleCommand command)
    {
        var id = await sender.Send(command with { TestId = testId });
        return TypedResults.Created($"/api/Tests/{testId}/metrics/{id}", id);
    }

    // ── Questions ─────────────────────────────────────────────────────────────

    [EndpointSummary("List Questions (with Options and OptionPoints)")]
    [EndpointDescription(
        "Returns all non-deleted questions for this test, ordered by OrderIndex. " +
        "Each question includes its options and the full OptionPoint mappings to TestVariables. " +
        "Intended for the admin/staff blueprint builder — scoring weights are included.")]
    public static async Task<Ok<List<QuestionWithOptionsDto>>> GetTestQuestions(
        ISender sender, int testId)
        => TypedResults.Ok(await sender.Send(new GetTestQuestionsQuery(testId)));

    [EndpointSummary("Create a Question (Aggregate Root)")]
    [EndpointDescription(
        "Creates a Question along with all its Options and OptionPoints in a single transaction. " +
        "All TestVariableIds referenced in OptionPoints must belong to this test. " +
        "Scoring weights are visible here because this endpoint is restricted to staff/admin.")]
    public static async Task<Created<int>> CreateQuestion(
        ISender sender, int testId, CreateQuestionCommand command)
    {
        var id = await sender.Send(command with { TestId = testId });
        return TypedResults.Created($"/api/Tests/{testId}/questions/{id}", id);
    }
}
