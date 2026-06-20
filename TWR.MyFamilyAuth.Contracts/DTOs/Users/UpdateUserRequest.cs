namespace TWR.MyFamilyAuth.Contracts.DTOs.Users;
public record UpdateUserRequest(
    string  FirstName,
    string  LastName,
    string  Email,
    string  Role,
    bool    IsActive,
    bool    IsWard,
    Guid?   GuardianId,
    bool    MustChangePassword,
    bool    PasswordChangeLocked,
    string? AvatarBase64,
    string? TimeZoneId,
    Guid?   PrimaryGroupId
);
