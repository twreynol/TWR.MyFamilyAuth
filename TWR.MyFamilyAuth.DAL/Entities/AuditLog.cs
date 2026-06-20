namespace TWR.MyFamilyAuth.DAL.Entities;

public class AuditLog
{
    public Guid      Id           { get; set; }
    public DateTime  Timestamp    { get; set; }
    public Guid?     FamilyUserId { get; set; }
    public string    Action       { get; set; } = string.Empty;
    public string?   IpAddress    { get; set; }
    public string?   AppClientId  { get; set; }
    public string?   Notes        { get; set; }
}
