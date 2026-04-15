using backend.Application.Questions.Commands.DeleteQuestion;
using backend.Application.Questions.Commands.UpdateQuestion;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Question management (resource-level operations).
///
/// PUT    /api/Questions/{id}   [SuperAdmin|TenantStaff]  — update question fields
/// DELETE /api/Questions/{id}   [SuperAdmin|TenantStaff]  — soft-delete question
///
/// Collection-level operations (GET list, POST create) live under
/// /api/Tests/{testId}/questions to make the parent context explicit.
/// </summary>
public class Questions : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPut(UpdateQuestion, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapDelete(DeleteQuestion, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    // ── PUT /api/Questions/{id} ───────────────────────────────────────────────

    [EndpointSummary("Update a Question (Full Aggregate)")]
    [EndpointDescription(
        "Full aggregate-root replacement: updates the question's scalar fields AND performs " +
        "a DELETE / UPDATE / ADD merge of its Options and each Option's OptionPoints. " +
        "Options present in the DB but absent from the request body are hard-deleted (cascade). " +
        "Options/OptionPoints with a non-zero Id are updated in-place. " +
        "Options/OptionPoints with no Id (or Id = 0) are inserted as new rows.")]
    public static async Task<NoContent> UpdateQuestion(
        ISender sender, int id, UpdateQuestionCommand command)
    {
        await sender.Send(command with { Id = id });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Questions/{id} ────────────────────────────────────────────

    [EndpointSummary("Delete a Question")]
    [EndpointDescription(
        "Soft-deletes the question. The row is retained in the database. " +
        "Existing AssignmentAnswers that reference this question are unaffected.")]
    public static async Task<NoContent> DeleteQuestion(ISender sender, int id)
    {
        await sender.Send(new DeleteQuestionCommand(id));
        return TypedResults.NoContent();
    }
}
