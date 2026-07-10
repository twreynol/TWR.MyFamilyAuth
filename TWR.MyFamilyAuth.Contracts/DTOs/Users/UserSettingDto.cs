namespace TWR.MyFamilyAuth.Contracts.DTOs.Users;

/// <summary>
/// A single setting/preference. Null AppClientId means a global setting
/// (timezone, notification channels, DND window); a non-null value scopes
/// the setting to one app (e.g. MyMedical's "ShowCycleLog").
/// </summary>
public record UserSettingDto(
    string? AppClientId,
    string  SettingKey,
    string  SettingValue
);

/// <summary>Request body for PUT /api/users/{id}/settings — a bulk upsert.</summary>
public record UpdateUserSettingsRequest(List<UserSettingDto> Settings);
