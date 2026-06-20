using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TWR.MyFamilyAuth.Contracts.DTOs.Admin;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using System.Security.Claims;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.AppAccess)]
[Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
public class AppAccessController : ControllerBase
{
    private readonly IDataAccess _data;
    public AppAccessController(IDataAccess data) => _data = data;

    [HttpGet("by-app/{appId:guid}")]
    public async Task<IActionResult> GetByApp(Guid appId)
    {
        var list = await _data.GetAppAccessByAppAsync(appId);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("by-user/{userId:guid}")]
    public async Task<IActionResult> GetByUser(Guid userId)
    {
        var list = await _data.GetAppAccessByUserAsync(userId);
        return Ok(list.Select(ToDto));
    }

    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] GrantAppAccessRequest request)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var access = await _data.GrantAppAccessAsync(new AppAccess
        {
            FamilyUserId    = request.FamilyUserId,
            RegisteredAppId = request.RegisteredAppId,
            AppRole         = request.AppRole,
            GrantedByUserId = callerId
        });
        return Ok(ToDto(access));
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] GrantAppAccessRequest request)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await _data.RevokeAppAccessAsync(request.FamilyUserId, request.RegisteredAppId, callerId);
        return ok ? Ok() : NotFound();
    }

    private static AppAccessDto ToDto(AppAccess a) => new(
        a.Id, a.FamilyUserId,
        a.User?.FullName ?? string.Empty,
        a.User?.Email ?? string.Empty,
        a.RegisteredAppId,
        a.App?.Name ?? string.Empty,
        a.AppRole, a.IsActive, a.GrantedAt, a.RevokedAt
    );
}
