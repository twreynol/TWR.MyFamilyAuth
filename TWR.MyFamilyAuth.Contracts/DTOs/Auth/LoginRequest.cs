namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record LoginRequest(
    string  Email,
    string  Password,
    string  AppClientId,        // which app the user is logging into — required
    string? DeviceTrustToken = null,
    string? TimeZoneId       = null,
    bool    RememberMe       = false
);
