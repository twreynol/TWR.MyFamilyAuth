namespace TWR.MyFamilyAuth.DAL.Entities;

public class DeviceTrust
{
    public Guid     Id           { get; set; }
    public Guid     FamilyUserId { get; set; }
    public string   TokenHash    { get; set; } = string.Empty; // SHA-256 of raw token
    public string   AppClientId  { get; set; } = string.Empty;
    public string?  DeviceLabel  { get; set; } // optional user-agent snippet
    public string?  IpAddress    { get; set; }
    public DateTime CreatedAt    { get; set; }
    public DateTime ExpiresAt    { get; set; }  // 90 days default
    public DateTime LastUsedAt   { get; set; }

    public FamilyUser User { get; set; } = null!;
}
