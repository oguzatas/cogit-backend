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

    [EndpointSummary("Update a Question")]
    [EndpointDescription(
        "Updates the core fields of a question (text, type, order, variableKey, settings). " +
        "Options and OptionPoints are managed via the parent test's questions endpoint.")]
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
