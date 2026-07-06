using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TWR.MyFamilyAuth.Contracts.DTOs.BuddyGrants;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

/// <summary>
/// V2 — Directional BuddyGrant management.
/// A grant flows ONE way: Grantor → Grantee → Permissions.
/// Granting access to your data does NOT give you access to the grantee's data.
/// </summary>
[ApiController]
[Authorize]
public class BuddyGrantsController : ControllerBase
{
    private readonly IDataAccess  _data;
    private readonly IMemoryCache _cache;

    public BuddyGrantsController(IDataAccess data, IMemoryCache cache)
    {
        _data  = data;
        _cache = cache;
    }

    // GET /api/users/{id}/grants/given — grants this user has given to others
    [HttpGet("api/users/{id:guid}/grants/given")]
    public async Task<IActionResult> GetGrantsGiven(Guid id)
    {
        if (!CallerIsAuthorized(id)) return Forbid();
        var grants = await _data.GetGrantsGivenAsync(id);
        return Ok(grants.Select(ToDto));
    }

    // GET /api/users/{id}/grants/received — grants this user has received from others
    [HttpGet("api/users/{id:guid}/grants/received")]
    public async Task<IActionResult> GetGrantsReceived(Guid id)
    {
        if (!CallerIsAuthorized(id)) return Forbid();
        var grants = await _data.GetGrantsReceivedAsync(id);
        return Ok(grants.Select(ToDto));
    }

    // GET /api/users/{id}/grants/check?granteeId=X&permission=Medical
    [HttpGet("api/users/{id:guid}/grants/check")]
    public async Task<IActionResult> CheckPermission(Guid id, [FromQuery] Guid granteeId, [FromQuery] string permission)
    {
        if (!CallerIsAuthorized(id) && !CallerIsAuthorized(granteeId)) return Forbid();
        var has = await _data.HasPermissionAsync(granteeId, id, permission);
        return Ok(new { GrantorId = id, GranteeId = granteeId, Permission = permission, HasPermission = has });
    }

    // POST /api/users/{id}/grants — create a grant (caller must be the grantor)
    [HttpPost("api/users/{id:guid}/grants")]
    public async Task<IActionResult> CreateGrant(Guid id, [FromBody] CreateBuddyGrantRequest request)
    {
        var callerId = GetCallerId();
        if (callerId != id) return Forbid(); // only the grantor can create their own grants

        if (id == request.GranteeId)
            return BadRequest("Cannot grant permissions to yourself.");

        if (request.Permissions is null || request.Permissions.Length == 0)
            return BadRequest("At least one permission is required.");

        var invalid = request.Permissions.Except(BuddyGrantPermissions.All).ToList();
        if (invalid.Count > 0)
            return BadRequest($"Invalid permissions: {string.Join(", ", invalid)}. Valid values: {string.Join(", ", BuddyGrantPermissions.All)}");

        // Upsert: if a grant already exists between these two, update the permissions
        var existing = await _data.GetGrantBetweenAsync(id, request.GranteeId);
        if (existing is not null)
        {
            existing.Permissions = request.Permissions.Distinct().ToArray();
            existing.IsActive    = true;
            existing.RevokedAt   = null;
            await _data.UpdateBuddyGrantAsync(existing);
            ClearAccessCacheForUser(request.GranteeId);
            return Ok(await BuildDto(existing));
        }

        var grant = await _data.CreateBuddyGrantAsync(new BuddyGrant
        {
            GrantorId   = id,
            GranteeId   = request.GranteeId,
            Permissions = request.Permissions.Distinct().ToArray()
        });

        ClearAccessCacheForUser(request.GranteeId);
        return Ok(await BuildDto(grant));
    }

    // PUT /api/users/{id}/grants/{grantId} — update permissions on an existing grant
    [HttpPut("api/users/{id:guid}/grants/{grantId:guid}")]
    public async Task<IActionResult> UpdateGrant(Guid id, Guid grantId, [FromBody] UpdateBuddyGrantRequest request)
    {
        var callerId = GetCallerId();
        if (callerId != id) return Forbid();

        var grant = await _data.GetGrantByIdAsync(grantId);
        if (grant is null || grant.GrantorId != id) return NotFound();

        var invalid = request.Permissions.Except(BuddyGrantPermissions.All).ToList();
        if (invalid.Count > 0)
            return BadRequest($"Invalid permissions: {string.Join(", ", invalid)}");

        grant.Permissions = request.Permissions.Distinct().ToArray();
        await _data.UpdateBuddyGrantAsync(grant);
        ClearAccessCacheForUser(grant.GranteeId);
        return Ok(ToDto(grant));
    }

    // POST /api/users/{id}/grants/{granteeId}/permissions/{permission}
    // Adds one permission to the existing grant (or creates it). Never removes other permissions.
    [HttpPost("api/users/{id:guid}/grants/{granteeId:guid}/permissions/{permission}")]
    public async Task<IActionResult> AddPermission(Guid id, Guid granteeId, string permission)
    {
        var callerId = GetCallerId();
        if (callerId != id) return Forbid();
        if (id == granteeId) return BadRequest("Cannot grant permissions to yourself.");
        if (!BuddyGrantPermissions.All.Contains(permission))
            return BadRequest($"Invalid permission '{permission}'. Valid: {string.Join(", ", BuddyGrantPermissions.All)}");

        var existing = await _data.GetGrantBetweenAsync(id, granteeId);
        if (existing is not null)
        {
            if (!existing.Permissions.Contains(permission))
                existing.Permissions = [.. existing.Permissions, permission];
            existing.IsActive  = true;
            existing.RevokedAt = null;
            await _data.UpdateBuddyGrantAsync(existing);
            ClearAccessCacheForUser(granteeId);
            return Ok(await BuildDto(existing));
        }

        var grant = await _data.CreateBuddyGrantAsync(new BuddyGrant
        {
            GrantorId   = id,
            GranteeId   = granteeId,
            Permissions = [permission]
        });
        ClearAccessCacheForUser(granteeId);
        return Ok(await BuildDto(grant));
    }

    // DELETE /api/users/{id}/grants/{granteeId}/permissions/{permission}
    // Removes one permission from the grant. Deletes the grant if it becomes empty.
    [HttpDelete("api/users/{id:guid}/grants/{granteeId:guid}/permissions/{permission}")]
    public async Task<IActionResult> RemovePermission(Guid id, Guid granteeId, string permission)
    {
        var callerId = GetCallerId();
        if (callerId != id) return Forbid();

        var existing = await _data.GetGrantBetweenAsync(id, granteeId);
        if (existing is null) return NotFound();

        var remaining = existing.Permissions.Where(p => p != permission).ToArray();
        if (remaining.Length == 0)
        {
            await _data.RevokeBuddyGrantAsync(existing.Id, id);
        }
        else
        {
            existing.Permissions = remaining;
            await _data.UpdateBuddyGrantAsync(existing);
        }
        ClearAccessCacheForUser(granteeId);
        return Ok();
    }

    // DELETE /api/users/{id}/grants/{grantId} — revoke a grant (caller must be the grantor)
    [HttpDelete("api/users/{id:guid}/grants/{grantId:guid}")]
    public async Task<IActionResult> RevokeGrant(Guid id, Guid grantId)
    {
        var callerId = GetCallerId();
        if (callerId != id && !User.IsInRole(FamilyRoles.SuperAdmin)) return Forbid();

        var grant = await _data.GetGrantByIdAsync(grantId);
        if (grant is null || grant.GrantorId != id) return NotFound();

        var ok = await _data.RevokeBuddyGrantAsync(grantId, id);
        if (ok)
            ClearAccessCacheForUser(grant.GranteeId);

        return ok ? Ok() : NotFound();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private void ClearAccessCacheForUser(Guid userId)
    {
        var appClientIds = new[] { "mymedical", "myfinances", "thefamilyinfo", "mymessages" };
        foreach (var appId in appClientIds)
        {
            var cacheKey = $"access-list:{userId}:{appId}";
            _cache.Remove(cacheKey);
        }
    }

    private Guid GetCallerId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool CallerIsAuthorized(Guid userId)
    {
        var callerId = GetCallerId();
        return callerId == userId || User.IsInRole(FamilyRoles.SuperAdmin);
    }

    private static BuddyGrantDto ToDto(BuddyGrant g) => new(
        g.Id,
        g.GrantorId, g.Grantor?.FullName ?? string.Empty, g.Grantor?.Email ?? string.Empty,
        g.GranteeId, g.Grantee?.FullName ?? string.Empty, g.Grantee?.Email ?? string.Empty,
        g.Permissions, g.IsActive, g.GrantedAt, g.RevokedAt
    );

    private async Task<BuddyGrantDto> BuildDto(BuddyGrant g)
    {
        // If nav props weren't loaded, fetch the full grant
        if (g.Grantor is null || g.Grantee is null)
            g = await _data.GetGrantByIdAsync(g.Id) ?? g;
        return ToDto(g);
    }
}

/// <summary>Valid permission values — enforced on create/update.</summary>
public static class BuddyGrantPermissions
{
    public const string Medical   = "Medical";
    public const string Info      = "Info";
    public const string Messaging = "Messaging";
    public const string Finances  = "Finances";
    public const string Admin     = "Admin";

    public static readonly string[] All = [Medical, Info, Messaging, Finances, Admin];
}
