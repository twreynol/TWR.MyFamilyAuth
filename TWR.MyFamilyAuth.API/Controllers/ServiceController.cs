using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

/// <summary>
/// Service-to-service endpoints for trusted partner applications (e.g. TWR.TheFamilyInfo).
/// Requires the X-Service-Key header matching the configured ServiceApiKey.
/// These endpoints bypass user-level auth and are intended for machine-to-machine calls only.
/// </summary>
[ApiController]
[Route("api/service")]
public class ServiceController : ControllerBase
{
    private readonly IDataAccess _data;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(IDataAccess data, IConfiguration config, ILogger<ServiceController> logger)
    {
        _data   = data;
        _config = config;
        _logger = logger;
    }

    private bool ValidateServiceKey()
    {
        var expected = _config["ServiceApiKey"];
        if (string.IsNullOrEmpty(expected)) return false;
        Request.Headers.TryGetValue("X-Service-Key", out var provided);
        return provided == expected;
    }

    /// <summary>
    /// POST /api/service/users — creates a user in MyFamilyAuth if they don't already exist.
    /// </summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] ServiceCreateUserRequest request)
    {
        if (!ValidateServiceKey()) return Unauthorized("Invalid service key.");

        var existing = await _data.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (existing is not null)
            return Ok(new { UserId = existing.Id, Created = false });

        var hash = BCrypt.Net.BCrypt.HashPassword(request.TempPassword);
        var user = await _data.CreateUserAsync(new FamilyUser
        {
            FirstName          = request.FirstName,
            LastName           = request.LastName,
            Email              = request.Email.Trim().ToLowerInvariant(),
            PasswordHash       = hash,
            Role               = request.Role ?? "User",
            MustChangePassword = request.MustChangePassword
        });

        _logger.LogInformation("ServiceController: created user {Email} (MFA ID {UserId})", user.Email, user.Id);
        return Ok(new { UserId = user.Id, Created = true });
    }

    /// <summary>
    /// POST /api/service/users/{email}/set-password — sets a user's password directly.
    /// Used when a TheFamilyInfo admin resets a user's password.
    /// </summary>
    [HttpPost("users/{email}/set-password")]
    public async Task<IActionResult> SetPassword(string email, [FromBody] ServiceSetPasswordRequest request)
    {
        if (!ValidateServiceKey()) return Unauthorized("Invalid service key.");

        var user = await _data.GetUserByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null) return NotFound("User not found in MyFamilyAuth.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _data.UpdatePasswordAsync(user.Id, hash);

        if (request.MustChangePassword)
        {
            user.MustChangePassword = true;
            await _data.UpdateUserAsync(user);
        }

        _logger.LogInformation("ServiceController: password set for {Email}", email);
        return Ok();
    }
}

public record ServiceCreateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string TempPassword,
    bool   MustChangePassword = true,
    string? Role = null
);

public record ServiceSetPasswordRequest(
    string NewPassword,
    bool   MustChangePassword = false
);
