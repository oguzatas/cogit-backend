using backend.Application.InviteCodes.Commands.CreateInviteCode;
using backend.Application.InviteCodes.Commands.RedeemInviteCode;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Invite-code lifecycle for Flow B (Self-Service Invite).
///
/// POST /api/InviteCodes         [SystemAdmin]  — generate a code for a Department
/// POST /api/InviteCodes/redeem  [Anonymous]    — new employee redeems code + registers
/// </summary>
public class InviteCodes : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Authenticated — SystemAdmin only.
        groupBuilder.MapPost(CreateInviteCode)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        // Public — no auth; the code itself is the credential.
        groupBuilder.MapPost(RedeemInviteCode, "redeem")
            .AllowAnonymous();
    }

    [EndpointSummary("Create an Invite Code (Flow B — Step 1)")]
    [EndpointDescription(
        "Generates a cryptographically random invite code for a Tenant Department. " +
        "Share the returned code with the prospective employee. Restricted to SystemAdmin.")]
    public static async Task<Created<CreateInviteCodeResult>> CreateInviteCode(
        ISender sender, CreateInviteCodeCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Created($"/api/InviteCodes/{result.Id}", result);
    }

    [EndpointSummary("Redeem an Invite Code (Flow B — Step 2)")]
    [EndpointDescription(
        "Public endpoint. The invitee submits their Name, Email, and the invite Code. " +
        "If the code is valid and unused, a TenantEmployee account is created automatically " +
        "under the Department encoded in the code. The code is marked as used immediately.")]
    public static async Task<Results<Created<int>, BadRequest<string>>> RedeemInviteCode(
        ISender sender, RedeemInviteCodeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/TenantEmployees/{id}", id);
    }
}
