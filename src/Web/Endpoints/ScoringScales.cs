using backend.Application.Common.Interfaces;
using backend.Application.ScoringScales.Commands.CreateScoringScale;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Manages global ScoringScales that belong to Tests.
/// Restricted to SuperAdmin — only platform staff may define scoring logic.
/// </summary>
public class ScoringScales : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // ── Authorization boundary ─────────────────────────────────────────────
        // Only SuperAdmins may create or manage global scoring scales.
        // TenantStaff and TenantEmployees are forbidden at the group level.
        groupBuilder.RequireAuthorization(policy =>
            policy.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPost(CreateScoringScale);
    }

    [EndpointSummary("Create a Scoring Scale")]
    [EndpointDescription(
        "Creates a new ScoringScale for the specified Test. " +
        "The FormulaExpression must reference valid [VariableKey] tokens that exist " +
        "as Questions on the Test and must be a valid mathematical expression. " +
        "Restricted to SuperAdmin.")]
    public static async Task<Results<Created<int>, ValidationProblem>> CreateScoringScale(
        ISender sender,
        CreateScoringScaleCommand command)
    {
        var id = await sender.Send(command);

        return TypedResults.Created($"/api/ScoringScales/{id}", id);
    }
}
