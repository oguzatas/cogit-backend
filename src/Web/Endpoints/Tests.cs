using backend.Application.Tests.Commands.CreateTest;
using backend.Application.Tests.Commands.DeleteTest;
using backend.Application.Tests.Commands.UpdateTest;
using backend.Application.Tests.Queries.GetTest;
using backend.Application.Tests.Queries.GetTests;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Test management (global — no tenant scope).
///
/// GET    /api/Tests        [SuperAdmin]  — list all tests
/// GET    /api/Tests/{id}   [SuperAdmin]  — single test
/// POST   /api/Tests        [SuperAdmin]  — create test
/// PUT    /api/Tests/{id}   [SuperAdmin]  — update test (name, description, isPublished)
/// DELETE /api/Tests/{id}   [SuperAdmin]  — soft-delete test
/// </summary>
public class Tests : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
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
    }

    // ── GET /api/Tests ────────────────────────────────────────────────────────

    [EndpointSummary("List all Tests")]
    [EndpointDescription("Returns all non-deleted tests ordered by name.")]
    public static async Task<Ok<List<TestDto>>> GetTests(ISender sender)
    {
        var result = await sender.Send(new GetTestsQuery());
        return TypedResults.Ok(result);
    }

    // ── GET /api/Tests/{id} ───────────────────────────────────────────────────

    [EndpointSummary("Get a Test")]
    [EndpointDescription("Returns a single test by id.")]
    public static async Task<Ok<TestDto>> GetTest(ISender sender, int id)
    {
        var result = await sender.Send(new GetTestQuery(id));
        return TypedResults.Ok(result);
    }

    // ── POST /api/Tests ───────────────────────────────────────────────────────

    [EndpointSummary("Create a Test")]
    [EndpointDescription("Creates a new test. Name must be unique. IsPublished defaults to false.")]
    public static async Task<Created<int>> CreateTest(ISender sender, CreateTestCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Tests/{id}", id);
    }

    // ── PUT /api/Tests/{id} ───────────────────────────────────────────────────

    [EndpointSummary("Update a Test")]
    [EndpointDescription("Updates name, description, and isPublished for the specified test.")]
    public static async Task<NoContent> UpdateTest(ISender sender, int id, UpdateTestCommand command)
    {
        await sender.Send(command with { Id = id });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Tests/{id} ────────────────────────────────────────────────

    [EndpointSummary("Delete a Test")]
    [EndpointDescription("Soft-deletes the specified test. The record is retained in the database.")]
    public static async Task<NoContent> DeleteTest(ISender sender, int id)
    {
        await sender.Send(new DeleteTestCommand(id));
        return TypedResults.NoContent();
    }
}
