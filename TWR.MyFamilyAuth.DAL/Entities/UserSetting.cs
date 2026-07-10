namespace TWR.MyFamilyAuth.DAL.Entities;

/// <summary>
/// A single per-user setting/preference. Null <see cref="AppClientId"/> means a global
/// setting (timezone, notification channels, DND window); a non-null value scopes the
/// setting to one app (e.g. MyMedical's "ShowCycleLog"). Value is always stored as a
/// string — the consuming app parses it to bool/time/enum as appropriate.
/// </summary>
public class UserSetting
{
    public Guid     Id            { get; set; }
    public Guid     FamilyUserId  { get; set; }
    public string?  AppClientId   { get; set; }
    public string   SettingKey    { get; set; } = string.Empty;
    public string   SettingValue  { get; set; } = string.Empty;
    public DateTime UpdatedUtc    { get; set; }

    public FamilyUser User { get; set; } = null!;
}
