using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<FamilyGroup?> GetGroupByIdAsync(Guid id)
    {
        using var db = CreateContext();
        try { return await db.FamilyGroups.Include(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == id); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching group {Id}", id); throw; }
    }

    public async Task<List<FamilyGroup>> GetGroupsByUserAsync(Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            return await db.FamilyGroups
                .Where(g => g.Members.Any(m => m.FamilyUserId == familyUserId) || g.PrimaryUsers.Any(u => u.Id == familyUserId))
                .Include(g => g.Members).ThenInclude(m => m.User)
                .OrderBy(g => g.Name)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching groups for user {Id}", familyUserId); throw; }
    }

    public async Task<List<FamilyGroup>> GetAllGroupsAsync()
    {
        using var db = CreateContext();
        try { return await db.FamilyGroups.Include(g => g.Members).OrderBy(g => g.Name).ToListAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Error listing groups"); throw; }
    }

    public async Task<FamilyGroup> CreateGroupAsync(FamilyGroup group)
    {
        using var db = CreateContext();
        try { group.CreatedAt = DateTime.UtcNow; db.FamilyGroups.Add(group); await db.SaveChangesAsync(); return group; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating group {Name}", group.Name); throw; }
    }

    public async Task<FamilyGroup?> UpdateGroupAsync(FamilyGroup group)
    {
        using var db = CreateContext();
        try
        {
            var existing = await db.FamilyGroups.FindAsync(group.Id);
            if (existing is null) return null;
            existing.Name      = group.Name;
            existing.IsActive  = group.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating group {Id}", group.Id); throw; }
    }

    public async Task<bool> DeactivateGroupAsync(Guid id)
    {
        using var db = CreateContext();
        try
        {
            var g = await db.FamilyGroups.FindAsync(id);
            if (g is null) return false;
            g.IsActive = false; g.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error deactivating group {Id}", id); throw; }
    }

    public async Task<GroupMember?> GetGroupMemberAsync(Guid familyUserId, Guid familyGroupId)
    {
        using var db = CreateContext();
        try { return await db.GroupMembers.FirstOrDefaultAsync(m => m.FamilyUserId == familyUserId && m.FamilyGroupId == familyGroupId); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching group member"); throw; }
    }

    public async Task<GroupMember> AddGroupMemberAsync(GroupMember member)
    {
        using var db = CreateContext();
        try { member.JoinedAt = DateTime.UtcNow; db.GroupMembers.Add(member); await db.SaveChangesAsync(); return member; }
        catch (Exception ex) { _logger.LogError(ex, "Error adding group member"); throw; }
    }

    public async Task<bool> RemoveGroupMemberAsync(Guid familyUserId, Guid familyGroupId)
    {
        using var db = CreateContext();
        try
        {
            var m = await db.GroupMembers.FirstOrDefaultAsync(x => x.FamilyUserId == familyUserId && x.FamilyGroupId == familyGroupId);
            if (m is null) return false;
            db.GroupMembers.Remove(m); await db.SaveChangesAsync(); return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error removing group member"); throw; }
    }
}
