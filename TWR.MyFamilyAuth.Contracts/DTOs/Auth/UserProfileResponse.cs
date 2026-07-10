using TWR.MyFamilyAuth.Contracts.DTOs.Users;

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
    DateTime? LastAccessedAt,
    // Global settings plus, when the caller passed ?appClientId=, that app's settings too.
    // Not embedded in the JWT (avoids token bloat/staleness) — callers fetch this once at
    // login/startup and cache client-side for the session.
    List<UserSettingDto>? Settings = null
);
