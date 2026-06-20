namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record ResetPasswordRequest(string Token, string NewPassword);
