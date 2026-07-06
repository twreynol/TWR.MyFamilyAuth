namespace TWR.MyFamilyAuth.Contracts.DTOs.Users;

/// <summary>
/// Returned by GET /api/users/lookup?email=x
/// RegisteredAppClientIds lists only active, non-revoked app registrations.
/// </summary>
public record UserLookupResult(
    Guid     UserId,
    string   FullName,
    string   Email,
    string[] RegisteredAppClientIds
);
