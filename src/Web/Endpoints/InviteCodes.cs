using backend.Application.InviteCodes.Commands.CreateInviteCode;
using backend.Application.InviteCodes.Commands.RedeemInviteCode;
using backend.Application.InviteCodes.Commands.RevokeInviteCode;
using backend.Application.InviteCodes.Queries.GetActiveInviteCodes;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Invite-code lifecycle for Flow B (Self-Service Invite).
///
/// GET    /api/InviteCodes?tenantId={}&departmentId={}   [SuperAdmin]  — active codes for a department
/// POST   /api/InviteCodes                               [SuperAdmin]  — generate a new code
/// DELETE /api/InviteCodes/{id}/revoke                   [SuperAdmin]  — revoke a code
/// POST   /api/InviteCodes/redeem                        [Anonymous]   — employee redeems code
/// </summary>
public class InviteCodes : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetActiveInviteCodes)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPost(CreateInviteCode)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapDelete(RevokeInviteCode, "{id}/revoke")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        // Public — no auth; the code itself is the credential.
        groupBuilder.MapPost(RedeemInviteCode, "redeem")
            .AllowAnonymous();
    }

    // ── GET /api/InviteCodes?tenantId={}&departmentId={} ─────────────────────

    [EndpointSummary("List Active Invite Codes for a Department")]
    [EndpointDescription(
        "Returns all invite codes for the specified department that are not revoked, " +
        "not expired, and have not reached their usage limit.")]
    public static async Task<Ok<List<InviteCodeDto>>> GetActiveInviteCodes(
        ISender sender, int tenantId, int departmentId)
    {
        var result = await sender.Send(new GetActiveInviteCodesQuery(tenantId, departmentId));
        return TypedResults.Ok(result);
    }

    // ── POST /api/InviteCodes ─────────────────────────────────────────────────

    [EndpointSummary("Create an Invite Code (Flow B — Step 1)")]
    [EndpointDescription(
        "Generates a cryptographically random invite code for a Tenant Department. " +
        "Optionally configure a per-code usage limit (maxUses) and/or an expiry date (expiresAt). " +
        "Share the returned code with prospective employees.")]
    public static async Task<Created<CreateInviteCodeResult>> CreateInviteCode(
        ISender sender, CreateInviteCodeCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Created($"/api/InviteCodes/{result.Id}", result);
    }

    // ── DELETE /api/InviteCodes/{id}/revoke ───────────────────────────────────

    [EndpointSummary("Revoke an Invite Code")]
    [EndpointDescription(
        "Permanently disables the invite code. Any further redemption attempts will be rejected. " +
        "The operation is idempotent — revoking an already-revoked code is a no-op.")]
    public static async Task<NoContent> RevokeInviteCode(ISender sender, int id)
    {
        await sender.Send(new RevokeInviteCodeCommand(id));
        return TypedResults.NoContent();
    }

    // ── POST /api/InviteCodes/redeem ──────────────────────────────────────────

    [EndpointSummary("Redeem an Invite Code (Flow B — Step 2)")]
    [EndpointDescription(
        "Public endpoint — no authentication required. " +
        "The invitee submits their FullName, Email, and the invite Code. " +
        "If valid, a TenantEmployee is created under the Department encoded in the code " +
        "and the usage counter is incremented. " +
        "Rejected if the code is revoked, expired, or has reached its MaxUses limit.")]
    public static async Task<Created<int>> RedeemInviteCode(
        ISender sender, RedeemInviteCodeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/TenantEmployees/{id}", id);
    }
}
