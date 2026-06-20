namespace TWR.MyFamilyAuth.DAL.Entities;

public class RegisteredApp
{
    public Guid     Id               { get; set; }
    public string   Name             { get; set; } = string.Empty;
    public string   ClientId         { get; set; } = string.Empty;
    public string   ClientSecretHash { get; set; } = string.Empty;
    public string   AllowedOrigins   { get; set; } = "[]";
    public bool     IsActive         { get; set; } = true;
    public bool     Requires2FA      { get; set; }
    public DateTime RegisteredAt     { get; set; }
    public DateTime? UpdatedAt       { get; set; }
}
