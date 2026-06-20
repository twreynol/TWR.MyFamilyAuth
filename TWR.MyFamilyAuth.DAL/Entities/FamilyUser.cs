namespace TWR.MyFamilyAuth.DAL.Entities;

public class FamilyUser
{
    public Guid      Id                   { get; set; }
    public string    FirstName            { get; set; } = string.Empty;
    public string    LastName             { get; set; } = string.Empty;
    public string    Email                { get; set; } = string.Empty;
    public string    PasswordHash         { get; set; } = string.Empty;
    public string    Role                 { get; set; } = FamilyRoles.User;
    public bool      IsActive             { get; set; } = true;
    public bool      IsWard               { get; set; }
    public Guid?     GuardianId           { get; set; }
    public bool      MustChangePassword   { get; set; }
    public bool      PasswordChangeLocked { get; set; }
    public string?   AvatarBase64         { get; set; }
    public string?   TimeZoneId           { get; set; }
    public Guid?     PrimaryGroupId       { get; set; }
    public DateTime  CreatedAt            { get; set; }
    public DateTime? UpdatedAt            { get; set; }
    public DateTime? LastAccessedAt       { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public FamilyUser?        Guardian          { get; set; }
    public FamilyGroup?       PrimaryGroup      { get; set; }
    public List<GroupMember>  GroupMemberships  { get; set; } = [];
    public List<BuddyGrant>   BuddyGrantsGiven  { get; set; } = [];
    public List<BuddyGrant>   BuddyGrantsReceived { get; set; } = [];
    public List<RefreshToken> RefreshTokens     { get; set; } = [];
}
