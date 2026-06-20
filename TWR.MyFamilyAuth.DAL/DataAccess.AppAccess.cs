using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<AppAccess?> GetAppAccessAsync(Guid familyUserId, Guid registeredAppId)
    {
        using var db = CreateContext();
        try
        {
            return await db.AppAccesses
                .Include(a => a.App)
                .FirstOrDefaultAsync(a => a.FamilyUserId == familyUserId && a.RegisteredAppId == registeredAppId && a.IsActive);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking app access for user {UserId}", familyUserId); throw; }
    }

    public async Task<List<AppAccess>> GetAppAccessByUserAsync(Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            return await db.AppAccesses
                .Include(a => a.App)
                .Where(a => a.FamilyUserId == familyUserId)
                .OrderBy(a => a.App.Name)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing app access for user {UserId}", familyUserId); throw; }
    }

    public async Task<List<AppAccess>> GetAppAccessByAppAsync(Guid registeredAppId)
    {
        using var db = CreateContext();
        try
        {
            return await db.AppAccesses
                .Include(a => a.User)
                .Where(a => a.RegisteredAppId == registeredAppId)
                .OrderBy(a => a.User.LastName).ThenBy(a => a.User.FirstName)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing users for app {AppId}", registeredAppId); throw; }
    }

    public async Task<AppAccess> GrantAppAccessAsync(AppAccess access)
    {
        using var db = CreateContext();
        try
        {
            var existing = await db.AppAccesses
                .FirstOrDefaultAsync(a => a.FamilyUserId == access.FamilyUserId && a.RegisteredAppId == access.RegisteredAppId);
            if (existing is not null)
            {
                existing.IsActive        = true;
                existing.AppRole         = access.AppRole;
                existing.GrantedAt       = DateTime.UtcNow;
                existing.GrantedByUserId = access.GrantedByUserId;
                existing.RevokedAt       = null;
                await db.SaveChangesAsync();
                return existing;
            }
            access.GrantedAt = DateTime.UtcNow;
            db.AppAccesses.Add(access);
            await db.SaveChangesAsync();
            return access;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error granting app access"); throw; }
    }

    public async Task<bool> RevokeAppAccessAsync(Guid familyUserId, Guid registeredAppId, Guid revokedByUserId)
    {
        using var db = CreateContext();
        try
        {
            var access = await db.AppAccesses
                .FirstOrDefaultAsync(a => a.FamilyUserId == familyUserId && a.RegisteredAppId == registeredAppId && a.IsActive);
            if (access is null) return false;
            access.IsActive  = false;
            access.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error revoking app access"); throw; }
    }

    public async Task GrantAppAccessToAllSuperAdminsAsync(Guid registeredAppId, Guid grantedByUserId)
    {
        using var db = CreateContext();
        try
        {
            var superAdmins = await db.FamilyUsers
                .Where(u => u.Role == Entities.FamilyRoles.SuperAdmin && u.IsActive)
                .ToListAsync();

            foreach (var admin in superAdmins)
            {
                var exists = await db.AppAccesses
                    .AnyAsync(a => a.FamilyUserId == admin.Id && a.RegisteredAppId == registeredAppId);
                if (exists) continue;

                db.AppAccesses.Add(new AppAccess
                {
                    FamilyUserId    = admin.Id,
                    RegisteredAppId = registeredAppId,
                    AppRole         = "SuperAdmin",
                    IsActive        = true,
                    GrantedAt       = DateTime.UtcNow,
                    GrantedByUserId = grantedByUserId
                });
            }
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error granting app access to super admins for app {AppId}", registeredAppId); throw; }
    }
}
