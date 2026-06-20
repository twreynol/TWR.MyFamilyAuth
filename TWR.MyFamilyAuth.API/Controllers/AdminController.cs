using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.Contracts.DTOs.Admin;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.Admin)]
[Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
public class AdminController : ControllerBase
{
    private readonly IDataAccess _data;
    public AdminController(IDataAccess data) => _data = data;

    [HttpGet("apps")]
    public async Task<IActionResult> GetApps()
    {
        var apps = await _data.GetAllRegisteredAppsAsync();
        return Ok(apps.Select(a => new RegisteredAppDto(a.Id, a.Name, a.ClientId, a.IsActive, a.Requires2FA, a.RegisteredAt)));
    }

    [HttpPost("apps")]
    public async Task<IActionResult> CreateApp([FromBody] CreateAppRequest request)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var secret   = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash     = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        var clientId = Guid.NewGuid().ToString("N")[..16];

        var app = await _data.CreateRegisteredAppAsync(new RegisteredApp
        {
            Name             = request.Name,
            ClientId         = clientId,
            ClientSecretHash = hash,
            AllowedOrigins   = request.AllowedOrigins,
            IsActive         = true
        });

        await _data.GrantAppAccessToAllSuperAdminsAsync(app.Id, callerId);

        return Ok(new CreateAppResponse(app.Id, app.Name, clientId, secret));
    }

    [HttpDelete("apps/{id:guid}")]
    public async Task<IActionResult> DeactivateApp(Guid id)
    {
        var ok = await _data.DeactivateRegisteredAppAsync(id);
        return ok ? Ok() : NotFound();
    }

    [HttpPatch("apps/{id:guid}/require-2fa")]
    public async Task<IActionResult> ToggleRequires2Fa(Guid id, [FromBody] Toggle2FaRequest request)
    {
        var app = await _data.UpdateRegisteredAppAsync(id, requires2Fa: request.Requires2FA);
        return app is null ? NotFound() : Ok(new RegisteredAppDto(app.Id, app.Name, app.ClientId, app.IsActive, app.Requires2FA, app.RegisteredAt));
    }

    [HttpPatch("apps/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAppPatch(Guid id)
    {
        var app = await _data.UpdateRegisteredAppAsync(id, isActive: false);
        return app is null ? NotFound() : Ok();
    }
}
