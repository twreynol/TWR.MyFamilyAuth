namespace TWR.MyFamilyAuth.DAL.Entities;

public class RefreshToken
{
    public Guid      Id           { get; set; }
    public Guid      FamilyUserId { get; set; }
    public string    TokenHash    { get; set; } = string.Empty; // SHA-256 of actual token
    public string?   AppClientId  { get; set; }
    public DateTime  CreatedAt    { get; set; }
    public DateTime  ExpiresAt    { get; set; }
    public bool      IsRevoked    { get; set; }

    public FamilyUser User { get; set; } = null!;
}
