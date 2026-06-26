namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;

public record UserProfileResponse(
    Guid     UserId,
    string   FirstName,
    string   LastName,
    string   FullName,
    string   Email,
    string   Role,
    bool     IsWard,
    Guid?    GuardianId,
    string[] Permissions,   // union of all permissions granted TO this user by others
    DateTime CreatedAt,
    DateTime? LastAccessedAt
);
