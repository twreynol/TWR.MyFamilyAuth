using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.DTOs.Users;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.Users)]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IDataAccess _data;
    public UsersController(IDataAccess data) => _data = data;

    [HttpGet]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        var users = await _data.GetAllUsersAsync(page, pageSize, search);
        var total = await _data.GetUserCountAsync(search);
        return Ok(new { Users = users.Select(ToDto), Total = total, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var user = await _data.GetUserByIdAsync(id);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    [HttpPost]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var existing = await _data.GetUserByEmailAsync(request.Email);
        if (existing is not null) return Conflict("Email already in use.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = await _data.CreateUserAsync(new FamilyUser
        {
            FirstName          = request.FirstName,
            LastName           = request.LastName,
            Email              = request.Email.Trim().ToLowerInvariant(),
            PasswordHash       = hash,
            Role               = request.Role,
            PrimaryGroupId     = request.PrimaryGroupId,
            MustChangePassword = request.MustChangePassword
        });
        return Ok(ToDto(user));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _data.GetUserByIdAsync(id);
        if (user is null) return NotFound();

        user.FirstName            = request.FirstName;
        user.LastName             = request.LastName;
        user.Email                = request.Email.Trim().ToLowerInvariant();
        user.Role                 = request.Role;
        user.IsActive             = request.IsActive;
        user.IsWard               = request.IsWard;
        user.GuardianId           = request.GuardianId;
        user.MustChangePassword   = request.MustChangePassword;
        user.PasswordChangeLocked = request.PasswordChangeLocked;
        user.AvatarBase64         = request.AvatarBase64;
        user.TimeZoneId           = request.TimeZoneId;
        user.PrimaryGroupId       = request.PrimaryGroupId;

        var updated = await _data.UpdateUserAsync(user);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = FamilyRoles.SuperAdmin)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var ok = await _data.DeactivateUserAsync(id);
        return ok ? Ok() : NotFound();
    }

    [HttpPost("{id:guid}/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        var callerId   = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (callerId != id && callerRole != FamilyRoles.SuperAdmin && callerRole != FamilyRoles.FamilyAdmin)
            return Forbid();

        var user = await _data.GetUserByIdAsync(id);
        if (user is null) return NotFound();

        if (user.PasswordChangeLocked && callerId == id)
            return BadRequest("Password changes are locked on this account.");

        if (callerId == id && !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _data.UpdatePasswordAsync(id, hash);
        await _data.WriteAuditLogAsync(new AuditLog { FamilyUserId = id, Action = "PasswordChanged" });
        return Ok();
    }

    [HttpGet("{id:guid}/trusted-devices")]
    public async Task<IActionResult> GetTrustedDevices(Guid id)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (callerId != id && !User.IsInRole(FamilyRoles.SuperAdmin)) return Forbid();
        var trusts = await _data.GetDeviceTrustsByUserAsync(id);
        return Ok(trusts.Select(t => new {
            t.Id, t.AppClientId, t.DeviceLabel, t.IpAddress,
            t.CreatedAt, t.LastUsedAt, t.ExpiresAt
        }));
    }

    [HttpDelete("{id:guid}/trusted-devices/{trustId:guid}")]
    public async Task<IActionResult> RevokeTrustedDevice(Guid id, Guid trustId)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (callerId != id && !User.IsInRole(FamilyRoles.SuperAdmin)) return Forbid();
        await _data.RevokeDeviceTrustAsync(trustId);
        return Ok();
    }

    // GET /api/users/lookup?email=x — find any registered user by email.
    // Any authenticated user may look up another by email (needed for BuddyDialog add flow).
    // Returns only non-Ward, active accounts. Returns 404 if no match.
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("email is required.");
        var user = await _data.GetUserByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive || user.IsWard) return NotFound();

        var access = await _data.GetAppAccessByUserAsync(user.Id);
        var appIds = access
            .Where(a => a.IsActive && a.RevokedAt is null)
            .Select(a => a.App?.ClientId ?? string.Empty)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();

        return Ok(new UserLookupResult(user.Id, user.FullName, user.Email, appIds));
    }

    private static FamilyUserDto ToDto(FamilyUser u) => new(
        u.Id, u.FirstName, u.LastName, u.FullName, u.Email, u.Role,
        u.IsActive, u.IsWard, u.GuardianId, u.MustChangePassword,
        u.PasswordChangeLocked, u.AvatarBase64, u.TimeZoneId,
        u.PrimaryGroupId, u.PrimaryGroup?.Name, u.CreatedAt, u.LastAccessedAt
    );
}
