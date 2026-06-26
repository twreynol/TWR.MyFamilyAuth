using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.Contracts.DTOs.Invitations;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

/// <summary>
/// V2 — Invitation management.
/// An Invitation lets a new user join the system. It does NOT create a BuddyGrant.
/// After accepting, the invitee must explicitly create grants to share their data.
/// </summary>
[ApiController]
[Route(ApiRoutes.Invitations)]
public class InvitationsController : ControllerBase
{
    private readonly IDataAccess _data;
    public InvitationsController(IDataAccess data) => _data = data;

    // POST /api/invitations — invite a new user (authenticated caller is the inviter)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var token = Guid.NewGuid().ToString("N"); // 32-char hex token
        var invitation = await _data.CreateInvitationAsync(new Invitation
        {
            InviteeEmail    = request.InviteeEmail.Trim().ToLowerInvariant(),
            DisplayName     = request.DisplayName,
            Token           = token,
            InvitedByUserId = callerId,
            ExpiresAt       = DateTime.UtcNow.AddDays(7)
        });

        return Ok(ToDto(invitation));
    }

    // GET /api/invitations/{token} — validate an invite link (anonymous — used on landing page)
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByToken(string token)
    {
        var invitation = await _data.GetInvitationByTokenAsync(token);
        if (invitation is null) return NotFound();
        if (invitation.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { Error = "This invitation has expired." });
        if (invitation.IsAccepted)
            return BadRequest(new { Error = "This invitation has already been accepted." });

        return Ok(ToDto(invitation));
    }

    private static InvitationDto ToDto(Invitation i) => new(
        i.Id,
        i.InviteeEmail,
        i.DisplayName,
        i.Token,
        i.InvitedByUserId,
        i.InvitedBy?.FullName ?? string.Empty,
        i.CreatedAt,
        i.ExpiresAt,
        i.IsAccepted,
        i.AcceptedAt,
        i.ExpiresAt < DateTime.UtcNow
    );
}
