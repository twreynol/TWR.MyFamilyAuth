using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<UserAccessCache?> GetAccessCacheAsync(Guid userId, string appClientId)
    {
        using var db = CreateContext();
        try
        {
            return await db.UserAccessCaches
                .FirstOrDefaultAsync(c => c.UserId == userId && c.AppClientId == appClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access cache for user {UserId} app {AppClientId}", userId, appClientId);
            throw;
        }
    }

    public async Task UpsertAccessCacheAsync(UserAccessCache cache)
    {
        using var db = CreateContext();
        try
        {
            var existing = await db.UserAccessCaches
                .FirstOrDefaultAsync(c => c.UserId == cache.UserId && c.AppClientId == cache.AppClientId);

            if (existing is null)
            {
                db.UserAccessCaches.Add(cache);
            }
            else
            {
                existing.GrantorIds = cache.GrantorIds;
                existing.UpdatedAt  = cache.UpdatedAt;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting access cache for user {UserId} app {AppClientId}", cache.UserId, cache.AppClientId);
            throw;
        }
    }

    public async Task DeleteAccessCacheAsync(Guid userId)
    {
        using var db = CreateContext();
        try
        {
            await db.UserAccessCaches
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting access cache for user {UserId}", userId);
            throw;
        }
    }
}
