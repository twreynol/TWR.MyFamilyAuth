namespace TWR.MyFamilyAuth.DAL.Entities;

public class TwoFactorChallenge
{
    public Guid     Id              { get; set; }
    public Guid     FamilyUserId    { get; set; }
    public Guid     RegisteredAppId { get; set; }
    public string   ChallengeToken  { get; set; } = string.Empty; // random GUID, sent to client
    public string   OtpHash         { get; set; } = string.Empty; // SHA-256 of 6-digit code
    public DateTime CreatedAt       { get; set; }
    public DateTime ExpiresAt       { get; set; }
    public bool     IsUsed          { get; set; }

    public FamilyUser    User { get; set; } = null!;
    public RegisteredApp App  { get; set; } = null!;
}
