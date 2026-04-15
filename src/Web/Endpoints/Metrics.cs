using backend.Application.ScoringScales.Commands.DeleteScoringScale;
using backend.Application.ScoringScales.Commands.UpdateScoringScale;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// ScoringScale (metric) resource-level operations.
///
/// PUT    /api/Metrics/{id}   [SuperAdmin|TenantStaff]  — update metric (name, formula)
/// DELETE /api/Metrics/{id}   [SuperAdmin|TenantStaff]  — soft-delete metric
///
/// Collection-level operations (GET list, POST create) live under
/// /api/Tests/{testId}/metrics to make the parent test context explicit.
/// </summary>
public class Metrics : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPut(UpdateMetric, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapDelete(DeleteMetric, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    // ── PUT /api/Metrics/{id} ─────────────────────────────────────────────────

    [EndpointSummary("Update a Scoring Metric")]
    [EndpointDescription(
        "Updates the name and NCalc FormulaExpression of a ScoringScale. " +
        "Use [VariableKey] syntax to reference TestVariables (e.g. '[LDR] + [EMP] * 0.5'). " +
        "The expression is validated for syntactic correctness before saving.")]
    public static async Task<NoContent> UpdateMetric(
        ISender sender, int id, UpdateScoringScaleCommand command)
    {
        await sender.Send(command with { Id = id });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Metrics/{id} ──────────────────────────────────────────────

    [EndpointSummary("Delete a Scoring Metric")]
    [EndpointDescription(
        "Soft-deletes the ScoringScale. Existing AssignmentResult rows are retained. " +
        "Future scoring runs for assignments on this test will not produce a result for this scale.")]
    public static async Task<NoContent> DeleteMetric(ISender sender, int id)
    {
        await sender.Send(new DeleteScoringScaleCommand(id));
        return TypedResults.NoContent();
    }
}
