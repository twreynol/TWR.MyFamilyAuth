namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record VerifyTwoFactorRequest(
    string ChallengeToken,
    string OtpCode,
    bool   TrustDevice = false  // if true, server issues a DeviceTrustToken
);
