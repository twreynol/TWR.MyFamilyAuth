namespace TWR.MyFamilyAuth.API.Services;
public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);
    Task SendWelcomeAsync(string toEmail, string toName);
    Task SendTwoFactorCodeAsync(string toEmail, string toName, string otpCode, string appName);
}
