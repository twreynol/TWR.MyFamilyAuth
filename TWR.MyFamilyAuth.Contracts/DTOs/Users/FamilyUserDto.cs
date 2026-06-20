namespace TWR.MyFamilyAuth.Contracts.DTOs.Users;

public record FamilyUserDto(
    Guid     Id,
    string   FirstName,
    string   LastName,
    string   FullName,
    string   Email,
    string   Role,
    bool     IsActive,
    bool     IsWard,
    Guid?    GuardianId,
    bool     MustChangePassword,
    bool     PasswordChangeLocked,
    string?  AvatarBase64,
    string?  TimeZoneId,
    Guid?    PrimaryGroupId,
    string?  PrimaryGroupName,
    DateTime CreatedAt,
    DateTime? LastAccessedAt
);
