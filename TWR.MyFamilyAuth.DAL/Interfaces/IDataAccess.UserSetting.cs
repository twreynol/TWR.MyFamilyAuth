using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    /// <summary>
    /// Returns a user's settings. When <paramref name="appClientId"/> is null, returns only
    /// global settings (AppClientId is null). When provided, returns global settings plus
    /// that app's settings merged together.
    /// </summary>
    Task<IEnumerable<UserSetting>> GetUserSettingsAsync(Guid userId, string? appClientId);

    /// <summary>
    /// Upserts a batch of settings for a user in one call (a settings page typically saves
    /// several at once). Each tuple's AppClientId may be null (global) or a specific app.
    /// </summary>
    Task UpsertUserSettingsAsync(Guid userId, IEnumerable<(string? AppClientId, string SettingKey, string SettingValue)> settings);
}
