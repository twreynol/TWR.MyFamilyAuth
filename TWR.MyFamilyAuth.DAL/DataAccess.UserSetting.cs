using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<IEnumerable<UserSetting>> GetUserSettingsAsync(Guid userId, string? appClientId)
    {
        using var db = CreateContext();
        try
        {
            var query = db.UserSettings.AsNoTracking().Where(s => s.FamilyUserId == userId);

            query = string.IsNullOrEmpty(appClientId)
                ? query.Where(s => s.AppClientId == null)
                : query.Where(s => s.AppClientId == null || s.AppClientId == appClientId);

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings for user {UserId} app {AppClientId}", userId, appClientId);
            throw;
        }
    }

    public async Task UpsertUserSettingsAsync(Guid userId, IEnumerable<(string? AppClientId, string SettingKey, string SettingValue)> settings)
    {
        using var db = CreateContext();
        try
        {
            var now = DateTime.UtcNow;
            foreach (var (appClientId, key, value) in settings)
            {
                var existing = await db.UserSettings.FirstOrDefaultAsync(s =>
                    s.FamilyUserId == userId && s.AppClientId == appClientId && s.SettingKey == key);

                if (existing is null)
                {
                    db.UserSettings.Add(new UserSetting
                    {
                        FamilyUserId = userId,
                        AppClientId  = appClientId,
                        SettingKey   = key,
                        SettingValue = value,
                        UpdatedUtc   = now
                    });
                }
                else
                {
                    existing.SettingValue = value;
                    existing.UpdatedUtc   = now;
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting settings for user {UserId}", userId);
            throw;
        }
    }
}
