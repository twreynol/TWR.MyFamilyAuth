using System.Security.Claims;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;
using Microsoft.Extensions.Options;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.Contracts.Helpers;

namespace TWR.MyFamilyAuth.API.AppServices;

public class AuthAppService : IAuthAppService
{
    private readonly IDataAccess   _data;
    private readonly IJwtService   _jwt;
    private readonly IEmailService _email;
    private readonly JwtSettings   _jwtSettings;
    private readonly ILogger<AuthAppService> _logger;

    public AuthAppService(IDataAccess data, IJwtService jwt, IEmailService email,
        IOptions<JwtSettings> jwtSettings, ILogger<AuthAppService> logger)
    {
        _data        = data;
        _jwt         = jwt;
        _email       = email;
        _jwtSettings = jwtSettings.Value;
        _logger      = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress)
    {
        var user = await _data.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive || user.IsWard) return null;
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

        if (!string.IsNullOrEmpty(request.TimeZoneId) && user.TimeZoneId != request.TimeZoneId)
        {
            user.TimeZoneId = request.TimeZoneId;
            await _data.UpdateUserAsync(user);
        }

        // Verify app exists and user has access
        var app = await _data.GetRegisteredAppByClientIdAsync(request.AppClientId);
        if (app is null) return null;

        var access = await _data.GetAppAccessAsync(user.Id, app.Id);
        if (access is null) return null;  // no access grant — deny silently like wrong password

        // 2FA check
        if (app.Requires2FA)
        {
            // Check if this device is already trusted
            bool deviceTrusted = false;
            if (!string.IsNullOrEmpty(request.DeviceTrustToken))
            {
                var hash  = HashString(request.DeviceTrustToken);
                var trust = await _data.GetDeviceTrustByHashAsync(hash);
                if (trust is not null && trust.FamilyUserId == user.Id && trust.AppClientId == app.ClientId)
                {
                    deviceTrusted = true;
                    await _data.UpdateDeviceTrustLastUsedAsync(trust.Id);
                }
            }

            if (!deviceTrusted)
            {
                // Generate OTP and challenge
                var otp       = new Random().Next(100000, 999999).ToString();
                var otpHash   = HashString(otp);
                var challenge = await _data.CreateTwoFactorChallengeAsync(new TwoFactorChallenge
                {
                    FamilyUserId    = user.Id,
                    RegisteredAppId = app.Id,
                    ChallengeToken  = Guid.NewGuid().ToString("N"),
                    OtpHash         = otpHash,
                    ExpiresAt       = DateTime.UtcNow.AddMinutes(10)
                });

                await _email.SendTwoFactorCodeAsync(user.Email, user.FullName, otp, app.Name);
                await _data.WriteAuditLogAsync(new AuditLog
                {
                    FamilyUserId = user.Id,
                    Action       = "TwoFactorChallengeSent",
                    IpAddress    = ipAddress,
                    AppClientId  = app.ClientId
                });

                // Return a pending response — Token is null
                return new LoginResponse(
                    Token:                   null,
                    RefreshToken:            null,
                    ExpiresAt:               DateTime.UtcNow,
                    UserId:                  user.Id,
                    FullName:                user.FullName,
                    Email:                   user.Email,
                    Role:                    user.Role,
                    MustChangePassword:      user.MustChangePassword,
                    RequiresTwoFactor:       true,
                    TwoFactorChallengeToken: challenge.ChallengeToken
                );
            }
        }

        var token        = _jwt.GenerateToken(user, app.ClientId, access.AppRole);
        string? refresh  = null;
        if (request.RememberMe)
        {
            var rawRefresh = _jwt.GenerateRefreshToken();
            refresh        = rawRefresh;
            await _data.StoreRefreshTokenAsync(new RefreshToken
            {
                FamilyUserId = user.Id,
                TokenHash    = _jwt.HashToken(rawRefresh),
                ExpiresAt    = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                AppClientId  = app.ClientId
            });
        }

        await _data.UpdateLastAccessedAsync(user.Id);
        await _data.WriteAuditLogAsync(new AuditLog { FamilyUserId = user.Id, Action = "Login", IpAddress = ipAddress, AppClientId = app.ClientId });

        return new LoginResponse(token, refresh, DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            user.Id, user.FullName, user.Email, user.Role, user.MustChangePassword);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken, string? ipAddress)
    {
        var hash    = _jwt.HashToken(refreshToken);
        var stored  = await _data.GetRefreshTokenByHashAsync(hash);
        if (stored is null || stored.ExpiresAt < DateTime.UtcNow) return null;

        await _data.RevokeRefreshTokenAsync(stored.Id);

        var user = stored.User;
        if (!user.IsActive || user.IsWard) return null;

        var token       = _jwt.GenerateToken(user, stored.AppClientId);
        var rawRefresh  = _jwt.GenerateRefreshToken();
        await _data.StoreRefreshTokenAsync(new RefreshToken
        {
            FamilyUserId = user.Id,
            TokenHash    = _jwt.HashToken(rawRefresh),
            ExpiresAt    = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            AppClientId  = stored.AppClientId
        });

        await _data.UpdateLastAccessedAsync(user.Id);
        await _data.WriteAuditLogAsync(new AuditLog { FamilyUserId = user.Id, Action = "TokenRefresh", IpAddress = ipAddress });

        return new LoginResponse(token, rawRefresh, DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            user.Id, user.FullName, user.Email, user.Role, user.MustChangePassword);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var hash   = _jwt.HashToken(refreshToken);
        var stored = await _data.GetRefreshTokenByHashAsync(hash);
        if (stored is not null)
        {
            await _data.RevokeRefreshTokenAsync(stored.Id);
            await _data.WriteAuditLogAsync(new AuditLog { FamilyUserId = stored.FamilyUserId, Action = "Logout" });
        }
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var user = await _data.GetUserByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive) return true; // don't reveal existence

        var token = new PasswordResetToken
        {
            FamilyUserId = user.Id,
            Token        = new Random().Next(100000, 999999).ToString(),
            ExpiresAt    = DateTime.UtcNow.AddMinutes(15)
        };
        await _data.CreatePasswordResetTokenAsync(token);
        await _email.SendPasswordResetAsync(user.Email, user.FullName, token.Token);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var token = await _data.GetPasswordResetTokenAsync(request.Token);
        if (token is null) return false;

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _data.UpdatePasswordAsync(token.FamilyUserId, hash);
        await _data.MarkPasswordResetTokenUsedAsync(token.Id);
        await _data.WriteAuditLogAsync(new AuditLog { FamilyUserId = token.FamilyUserId, Action = "PasswordReset" });
        return true;
    }

    public Task<ValidateTokenResponse> ValidateTokenAsync(string token)
    {
        var (valid, principal) = _jwt.ValidateToken(token);
        if (!valid || principal is null)
            return Task.FromResult(new ValidateTokenResponse(false, null, null, null, null, null, null));

        var userId  = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null;
        var groupId = Guid.TryParse(principal.FindFirstValue("family_group_id"), out var gg) ? gg : (Guid?)null;
        var given   = principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
        var family  = principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
        var appRole = principal.FindFirstValue("app_role");

        return Task.FromResult(new ValidateTokenResponse(
            true, userId,
            principal.FindFirstValue(ClaimTypes.Email),
            $"{given} {family}".Trim(),
            principal.FindFirstValue(ClaimTypes.Role),
            groupId,
            appRole));
    }

    public async Task<LoginResponse?> VerifyTwoFactorAsync(VerifyTwoFactorRequest request, string? ipAddress)
    {
        var challenge = await _data.GetTwoFactorChallengeAsync(request.ChallengeToken);
        if (challenge is null) return null;

        var otpHash = HashString(request.OtpCode);
        if (otpHash != challenge.OtpHash) return null;

        await _data.MarkChallengeUsedAsync(challenge.Id);

        var user = challenge.User;
        var app  = challenge.App;
        if (!user.IsActive || user.IsWard) return null;

        var access = await _data.GetAppAccessAsync(user.Id, app.Id);
        if (access is null) return null;

        var token = _jwt.GenerateToken(user, app.ClientId, access.AppRole);

        var rawRefresh = _jwt.GenerateRefreshToken();
        await _data.StoreRefreshTokenAsync(new RefreshToken
        {
            FamilyUserId = user.Id,
            TokenHash    = _jwt.HashToken(rawRefresh),
            ExpiresAt    = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            AppClientId  = app.ClientId
        });

        string? deviceTrustToken = null;
        if (request.TrustDevice)
        {
            deviceTrustToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            await _data.CreateDeviceTrustAsync(new DeviceTrust
            {
                FamilyUserId = user.Id,
                TokenHash    = HashString(deviceTrustToken),
                AppClientId  = app.ClientId,
                ExpiresAt    = DateTime.UtcNow.AddDays(90),
                IpAddress    = ipAddress
            });
        }

        await _data.UpdateLastAccessedAsync(user.Id);
        await _data.WriteAuditLogAsync(new AuditLog
        {
            FamilyUserId = user.Id,
            Action       = "TwoFactorVerified",
            IpAddress    = ipAddress,
            AppClientId  = app.ClientId
        });

        return new LoginResponse(
            Token:              token,
            RefreshToken:       rawRefresh,
            ExpiresAt:          DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            UserId:             user.Id,
            FullName:           user.FullName,
            Email:              user.Email,
            Role:               user.Role,
            MustChangePassword: user.MustChangePassword,
            DeviceTrustToken:   deviceTrustToken
        );
    }

    private static string HashString(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
