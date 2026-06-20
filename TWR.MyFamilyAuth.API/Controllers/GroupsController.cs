using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TWR.MyFamilyAuth.Contracts.DTOs.Groups;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route(ApiRoutes.Groups)]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly IDataAccess _data;
    public GroupsController(IDataAccess data) => _data = data;

    [HttpGet]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> List()
    {
        var groups = await _data.GetAllGroupsAsync();
        return Ok(groups.Select(ToDto));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();
        var groups = await _data.GetGroupsByUserAsync(userId);
        return Ok(groups.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var group = await _data.GetGroupByIdAsync(id);
        return group is null ? NotFound() : Ok(ToDto(group));
    }

    [HttpPost]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        var group = await _data.CreateGroupAsync(new FamilyGroup
        {
            Name          = request.Name,
            ParentGroupId = request.ParentGroupId
        });
        return Ok(ToDto(group));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupRequest request)
    {
        var existing = await _data.GetGroupByIdAsync(id);
        if (existing is null) return NotFound();
        existing.Name     = request.Name;
        existing.IsActive = request.IsActive;
        var updated = await _data.UpdateGroupAsync(existing);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = FamilyRoles.SuperAdmin)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var ok = await _data.DeactivateGroupAsync(id);
        return ok ? Ok() : NotFound();
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddGroupMemberRequest request)
    {
        var existing = await _data.GetGroupMemberAsync(request.FamilyUserId, id);
        if (existing is not null) return Conflict("User is already a member of this group.");

        var member = await _data.AddGroupMemberAsync(new GroupMember
        {
            FamilyUserId    = request.FamilyUserId,
            FamilyGroupId   = id,
            GroupRole       = request.GroupRole,
            IsLimitedMember = request.IsLimitedMember
        });
        return Ok(member);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Roles = $"{FamilyRoles.SuperAdmin},{FamilyRoles.FamilyAdmin}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var ok = await _data.RemoveGroupMemberAsync(userId, id);
        return ok ? Ok() : NotFound();
    }

    private static FamilyGroupDto ToDto(FamilyGroup g) => new(
        g.Id, g.Name, g.ParentGroupId, g.IsActive, g.CreatedAt,
        g.Members.Select(m => new GroupMemberDto(
            m.FamilyUserId,
            m.User?.FullName ?? string.Empty,
            m.User?.Email   ?? string.Empty,
            m.GroupRole,
            m.IsLimitedMember,
            m.JoinedAt
        )).ToList()
    );
}
