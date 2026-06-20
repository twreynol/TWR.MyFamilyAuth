namespace TWR.MyFamilyAuth.DAL.Entities;

public class PasswordResetToken
{
    public Guid     Id           { get; set; }
    public Guid     FamilyUserId { get; set; }
    public string   Token        { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }
    public DateTime ExpiresAt    { get; set; }
    public bool     IsUsed       { get; set; }

    public FamilyUser User { get; set; } = null!;
}
