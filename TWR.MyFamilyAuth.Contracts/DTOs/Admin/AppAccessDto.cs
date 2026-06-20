namespace TWR.MyFamilyAuth.Contracts.DTOs.Admin;

public record AppAccessDto(
    Guid      Id,
    Guid      FamilyUserId,
    string    UserFullName,
    string    UserEmail,
    Guid      RegisteredAppId,
    string    AppName,
    string?   AppRole,
    bool      IsActive,
    DateTime  GrantedAt,
    DateTime? RevokedAt
);
