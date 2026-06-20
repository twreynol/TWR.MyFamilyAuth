namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record LoginResponse(
    string?  Token,
    string?  RefreshToken,
    DateTime ExpiresAt,
    Guid     UserId,
    string   FullName,
    string   Email,
    string   Role,
    bool     MustChangePassword,
    bool     RequiresTwoFactor        = false,
    string?  TwoFactorChallengeToken  = null,
    string?  DeviceTrustToken         = null   // returned after successful 2FA verify; client stores in localStorage
);
