namespace TWR.MyFamilyAuth.Contracts.DTOs.Admin;

public record RegisteredAppDto(
    Guid          Id,
    string        Name,
    string        ClientId,
    bool          IsActive,
    bool          Requires2FA,
    List<string>  SupportedRoles,
    DateTime      RegisteredAt
);
