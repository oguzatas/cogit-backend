using backend.Application.TestVariables.Commands.DeleteTestVariable;
using backend.Application.TestVariables.Commands.UpdateTestVariable;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// TestVariable resource-level operations.
///
/// PUT    /api/Variables/{id}   [SuperAdmin|TenantStaff]  — update variable (name, key, defaultValue)
/// DELETE /api/Variables/{id}   [SuperAdmin|TenantStaff]  — soft-delete variable
///
/// Collection-level operations (GET list, POST create) live under
/// /api/Tests/{testId}/variables to make the parent test context explicit.
/// </summary>
public class Variables : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPut(UpdateVariable, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapDelete(DeleteVariable, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    // ── PUT /api/Variables/{id} ───────────────────────────────────────────────

    [EndpointSummary("Update a TestVariable")]
    [EndpointDescription(
        "Updates the name, NCalc key, and default value of a TestVariable. " +
        "Key must remain unique per test. " +
        "Warning: changing the Key will break any ScoringScale formula that references the old key.")]
    public static async Task<NoContent> UpdateVariable(
        ISender sender, int id, UpdateTestVariableCommand command)
    {
        await sender.Send(command with { Id = id });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Variables/{id} ────────────────────────────────────────────

    [EndpointSummary("Delete a TestVariable")]
    [EndpointDescription(
        "Soft-deletes the TestVariable. The record is retained in the database. " +
        "ScoringScale formulas that reference this variable's key will produce 0 for that term.")]
    public static async Task<NoContent> DeleteVariable(ISender sender, int id)
    {
        await sender.Send(new DeleteTestVariableCommand(id));
        return TypedResults.NoContent();
    }
}
