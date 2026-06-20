namespace TWR.MyFamilyAuth.DAL.Entities;

public class AppAccess
{
    public Guid      Id               { get; set; }
    public Guid      FamilyUserId     { get; set; }
    public Guid      RegisteredAppId  { get; set; }
    public string?   AppRole          { get; set; }  // optional app-specific role (e.g. "Owner", "Viewer")
    public bool      IsActive         { get; set; } = true;
    public DateTime  GrantedAt        { get; set; }
    public Guid      GrantedByUserId  { get; set; }
    public DateTime? RevokedAt        { get; set; }

    public FamilyUser     User         { get; set; } = null!;
    public RegisteredApp  App          { get; set; } = null!;
    public FamilyUser     GrantedBy    { get; set; } = null!;
}
