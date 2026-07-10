using TWR.MyFamilyAuth.Contracts.DTOs.Auth;

namespace TWR.MyFamilyAuth.Web.Services;

public interface IAuthService
{
    string? LastLoginError        { get; }
    string? PendingChallengeToken { get; }
    bool    MustChangePassword    { get; }
    Task<bool> LoginAsync(LoginRequest request);
    Task<bool> VerifyTwoFactorAsync(string otpCode, bool trustDevice);
    Task<bool> TryRefreshAsync();
    Task LogoutAsync();
    string? GetToken();
    Task<bool> ForgotPasswordAsync(string email);
    Task<bool> ResetPasswordAsync(string token, string newPassword);
}
