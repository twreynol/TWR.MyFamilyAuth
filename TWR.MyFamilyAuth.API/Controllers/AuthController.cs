using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.Auth)]
public class AuthController : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, string> AppPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "mymedical",     "Medical"   },
        { "myfinances",    "Finances"  },
        { "thefamilyinfo", "Info"      },
        { "mymessages",    "Messaging" },
    };

    private readonly IAuthAppService _auth;
    private readonly IDataAccess     _data;
    private readonly IMemoryCache    _cache;

    public AuthController(IAuthAppService auth, IDataAccess data, IMemoryCache cache)
    {
        _auth  = auth;
        _data  = data;
        _cache = cache;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ip);
        return result is null ? Unauthorized("Invalid email or password.") : Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RefreshAsync(request.RefreshToken, ip);
        return result is null ? Unauthorized("Refresh token invalid or expired.") : Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return Ok();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _auth.ForgotPasswordAsync(request.Email);
        return Ok(new { message = "If that email exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ok = await _auth.ResetPasswordAsync(request);
        return ok ? Ok() : BadRequest("Invalid or expired reset code.");
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromBody] ValidateTokenRequest request)
    {
        var result = await _auth.ValidateTokenAsync(request.Token);
        return Ok(result);
    }

    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.VerifyTwoFactorAsync(request, ip);
        return result is null ? Unauthorized("Invalid or expired verification code.") : Ok(result);
    }

    // V2 — GET /api/auth/me — full profile + all permissions granted to the caller.
    // ?appClientId=X additionally returns that app's settings alongside global settings.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me([FromQuery] string? appClientId)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user     = await _data.GetUserByIdAsync(callerId);
        if (user is null) return NotFound();

        var permissions = await _data.GetPermissionsForUserAsync(callerId);
        var settings     = await _data.GetUserSettingsAsync(callerId, appClientId);

        return Ok(new UserProfileResponse(
            user.Id, user.FirstName, user.LastName, user.FullName,
            user.Email, user.Role, user.IsWard, user.GuardianId,
            [.. permissions],
            user.CreatedAt, user.LastAccessedAt,
            [.. settings.Select(s => new Contracts.DTOs.Users.UserSettingDto(s.AppClientId, s.SettingKey, s.SettingValue))]
        ));
    }

    // V2 — GET /api/auth/access-list?appClientId=mymedical
    // Returns the list of user IDs whose data the caller is allowed to read in the given app.
    // Always rebuilds from BuddyGrants (no caching) to ensure correctness during V2 migration.
    [HttpGet("access-list")]
    [Authorize]
    public async Task<IActionResult> AccessList([FromQuery] string appClientId)
    {
        if (string.IsNullOrWhiteSpace(appClientId))
            return BadRequest("appClientId is required.");

        if (!AppPermissions.TryGetValue(appClientId, out var permission))
            return BadRequest($"Unknown appClientId '{appClientId}'.");

        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Always rebuild from BuddyGrants (no caching) — correctness over performance during V2 migration
        var grants = await _data.GetGrantsReceivedAsync(callerId);
        var grantorIds = grants
            .Where(g => g.IsActive && g.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            .Select(g => g.GrantorId)
            .ToArray();

        var wardIds = await GetWardIdsAsync(callerId);
        var accessList = grantorIds.Concat(wardIds).Distinct().ToArray();

        // Resolve grantor emails so callers can map to their own local user records
        // without needing a separate id-mapping column (avoids backfill dependency).
        var grantorEmails = new Dictionary<Guid, string>();
        foreach (var grantorId in grantorIds)
        {
            var grantor = await _data.GetUserByIdAsync(grantorId);
            if (grantor is not null)
                grantorEmails[grantorId] = grantor.Email;
        }

        var response = new AccessListResponse(callerId, appClientId, grantorIds, wardIds, accessList, grantorEmails);

        return Ok(response);
    }

    private async Task<Guid[]> GetWardIdsAsync(Guid guardianId)
    {
        var wards = await _data.GetWardsByGuardianAsync(guardianId);
        return wards.Select(u => u.Id).ToArray();
    }

    private void CacheResponse(string key, AccessListResponse response, DateTime updatedAt)
    {
        var remaining = updatedAt.Add(CacheTtl) - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero)
            _cache.Set(key, response, remaining);
    }
}
