using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<List<BuddyGrant>> GetGrantsGivenAsync(Guid grantorId)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants
                .Include(g => g.Grantee)
                .Where(g => g.GrantorId == grantorId && g.IsActive)
                .OrderBy(g => g.Grantee.LastName).ThenBy(g => g.Grantee.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grants given by {GrantorId}", grantorId);
            throw;
        }
    }

    public async Task<List<BuddyGrant>> GetGrantsReceivedAsync(Guid granteeId)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants
                .Include(g => g.Grantor)
                .Where(g => g.GranteeId == granteeId && g.IsActive)
                .OrderBy(g => g.Grantor.LastName).ThenBy(g => g.Grantor.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grants received by {GranteeId}", granteeId);
            throw;
        }
    }

    public async Task<BuddyGrant?> GetGrantByIdAsync(Guid grantId)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants
                .Include(g => g.Grantor)
                .Include(g => g.Grantee)
                .FirstOrDefaultAsync(g => g.Id == grantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grant {GrantId}", grantId);
            throw;
        }
    }

    public async Task<BuddyGrant?> GetGrantBetweenAsync(Guid grantorId, Guid granteeId)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants
                .FirstOrDefaultAsync(g => g.GrantorId == grantorId && g.GranteeId == granteeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grant between {GrantorId} and {GranteeId}", grantorId, granteeId);
            throw;
        }
    }

    public async Task<BuddyGrant> CreateBuddyGrantAsync(BuddyGrant grant)
    {
        using var db = CreateContext();
        try
        {
            grant.GrantedAt = DateTime.UtcNow;
            grant.IsActive  = true;
            db.BuddyGrants.Add(grant);
            await db.SaveChangesAsync();

            // Clear the access cache for the grantee so new permissions take effect immediately
            await db.UserAccessCaches
                .Where(c => c.UserId == grant.GranteeId)
                .ExecuteDeleteAsync();

            return grant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating buddy grant from {GrantorId} to {GranteeId}", grant.GrantorId, grant.GranteeId);
            throw;
        }
    }

    public async Task<bool> UpdateBuddyGrantAsync(BuddyGrant grant)
    {
        using var db = CreateContext();
        try
        {
            db.BuddyGrants.Update(grant);
            var success = await db.SaveChangesAsync() > 0;

            // Clear the access cache for the grantee so permission changes take effect immediately
            if (success)
            {
                await db.UserAccessCaches
                    .Where(c => c.UserId == grant.GranteeId)
                    .ExecuteDeleteAsync();
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating buddy grant {GrantId}", grant.Id);
            throw;
        }
    }

    public async Task<bool> RevokeBuddyGrantAsync(Guid grantId, Guid grantorId)
    {
        using var db = CreateContext();
        try
        {
            var grant = await db.BuddyGrants
                .FirstOrDefaultAsync(g => g.Id == grantId && g.GrantorId == grantorId && g.IsActive);
            if (grant is null) return false;

            grant.IsActive  = false;
            grant.RevokedAt = DateTime.UtcNow;
            var success = await db.SaveChangesAsync() > 0;

            // Clear the access cache for the grantee so permission changes take effect immediately
            if (success)
            {
                await db.UserAccessCaches
                    .Where(c => c.UserId == grant.GranteeId)
                    .ExecuteDeleteAsync();
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking grant {GrantId}", grantId);
            throw;
        }
    }

    public async Task<bool> HasPermissionAsync(Guid granteeId, Guid grantorId, string permission)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants.AnyAsync(g =>
                g.GranteeId == granteeId &&
                g.GrantorId == grantorId &&
                g.IsActive  &&
                g.Permissions.Contains(permission));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for grantee {GranteeId} on grantor {GrantorId}",
                permission, granteeId, grantorId);
            throw;
        }
    }

    public async Task<List<string>> GetPermissionsForUserAsync(Guid granteeId)
    {
        using var db = CreateContext();
        try
        {
            var grants = await db.BuddyGrants
                .Where(g => g.GranteeId == granteeId && g.IsActive)
                .Select(g => g.Permissions)
                .ToListAsync();

            return grants.SelectMany(p => p).Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {GranteeId}", granteeId);
            throw;
        }
    }
}
