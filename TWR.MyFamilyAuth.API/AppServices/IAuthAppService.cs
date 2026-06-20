using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
namespace TWR.MyFamilyAuth.API.AppServices;
public interface IAuthAppService
{
    Task<LoginResponse?>         LoginAsync(LoginRequest request, string? ipAddress);
    Task<LoginResponse?>         RefreshAsync(string refreshToken, string? ipAddress);
    Task                         LogoutAsync(string refreshToken);
    Task<bool>                   ForgotPasswordAsync(string email);
    Task<bool>                   ResetPasswordAsync(ResetPasswordRequest request);
    Task<ValidateTokenResponse>  ValidateTokenAsync(string token);
    Task<LoginResponse?>         VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress);
}
