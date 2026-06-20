using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.Helpers;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.Tests.Auth;

/// <summary>
/// Unit tests for the 2FA / device trust flows in AuthAppService.
/// </summary>
public class TwoFactorTests
{
    // ── Fixed test data ──────────────────────────────────────────────────────────

    private static readonly Guid   UserId    = Guid.NewGuid();
    private static readonly Guid   AppId     = Guid.NewGuid();
    private static readonly Guid   AccessId  = Guid.NewGuid();
    private const           string Email     = "2fa@example.com";
    private const           string Password  = "P@ssw0rd!";
    private const           string AppClient = "myfinances";

    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static FamilyUser ActiveUser() => new()
    {
        Id           = UserId,
        FirstName    = "Jane",
        LastName     = "Doe",
        Email        = Email,
        PasswordHash = PasswordHash,
        Role         = FamilyRoles.User,
        IsActive     = true,
        IsWard       = false
    };

    private static RegisteredApp AppWith2FA(bool requires2Fa = true) => new()
    {
        Id          = AppId,
        Name        = "MyFinances",
        ClientId    = AppClient,
        IsActive    = true,
        Requires2FA = requires2Fa
    };

    private static AppAccess ActiveAccess() => new()
    {
        Id              = AccessId,
        FamilyUserId    = UserId,
        RegisteredAppId = AppId,
        AppRole         = "User",
        IsActive        = true,
        GrantedAt       = DateTime.UtcNow,
        GrantedByUserId = UserId
    };

    private static JwtSettings TestJwtSettings() => new()
    {
        Issuer                 = "test-issuer",
        Audience               = "test-audience",
        Secret                 = "test-secret-key-must-be-at-least-32-chars-long!",
        ExpiryMinutes          = 60,
        RefreshTokenExpiryDays = 30
    };

    // Reproduce AuthAppService.HashString so we can build matching hashes in tests.
    private static string HashString(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static (AuthAppService svc, Mock<IDataAccess> data, Mock<IEmailService> email)
        Build(FamilyUser? user = null, RegisteredApp? app = null, AppAccess? access = null)
    {
        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();

        data.Setup(d => d.GetUserByEmailAsync(Email)).ReturnsAsync(user);
        data.Setup(d => d.GetRegisteredAppByClientIdAsync(AppClient)).ReturnsAsync(app);
        data.Setup(d => d.GetAppAccessAsync(UserId, AppId)).ReturnsAsync(access);

        data.Setup(d => d.UpdateUserAsync(It.IsAny<FamilyUser>())).ReturnsAsync((FamilyUser u) => u);
        data.Setup(d => d.UpdateLastAccessedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);
        data.Setup(d => d.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).ReturnsAsync((RefreshToken t) => t);
        data.Setup(d => d.CreateTwoFactorChallengeAsync(It.IsAny<TwoFactorChallenge>()))
            .ReturnsAsync((TwoFactorChallenge c) => { c.Id = Guid.NewGuid(); return c; });
        data.Setup(d => d.MarkChallengeUsedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.GetDeviceTrustByHashAsync(It.IsAny<string>())).ReturnsAsync((DeviceTrust?)null);
        data.Setup(d => d.UpdateDeviceTrustLastUsedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.CreateDeviceTrustAsync(It.IsAny<DeviceTrust>())).ReturnsAsync((DeviceTrust t) => t);

        email.Setup(e => e.SendTwoFactorCodeAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var jwtOptions = Options.Create(TestJwtSettings());
        var jwt        = new JwtService(jwtOptions);
        var svc        = new AuthAppService(data.Object, jwt, email.Object, jwtOptions,
                             NullLogger<AuthAppService>.Instance);
        return (svc, data, email);
    }

    private static LoginRequest ValidRequest(string? deviceTrustToken = null) =>
        new(Email, Password, AppClient, DeviceTrustToken: deviceTrustToken);

    // ── Login → 2FA challenge path ───────────────────────────────────────────────

    [Fact]
    public async Task Login_AppRequires2FA_NoDeviceTrust_ReturnsChallengeResponse()
    {
        var (svc, _, _) = Build(user: ActiveUser(), app: AppWith2FA(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), null);

        Assert.NotNull(result);
        Assert.True(result.RequiresTwoFactor);
        Assert.Null(result.Token);
        Assert.NotNull(result.TwoFactorChallengeToken);
    }

    [Fact]
    public async Task Login_AppRequires2FA_NoDeviceTrust_OtpEmailIsSent()
    {
        var (svc, _, email) = Build(user: ActiveUser(), app: AppWith2FA(), access: ActiveAccess());

        await svc.LoginAsync(ValidRequest(), null);

        email.Verify(e => e.SendTwoFactorCodeAsync(Email, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Login_AppRequires2FA_NoDeviceTrust_ChallengeIsStoredInDb()
    {
        var (svc, data, _) = Build(user: ActiveUser(), app: AppWith2FA(), access: ActiveAccess());

        await svc.LoginAsync(ValidRequest(), null);

        data.Verify(d => d.CreateTwoFactorChallengeAsync(It.Is<TwoFactorChallenge>(c =>
            c.FamilyUserId == UserId &&
            !string.IsNullOrEmpty(c.ChallengeToken) &&
            !string.IsNullOrEmpty(c.OtpHash))), Times.Once);
    }

    [Fact]
    public async Task Login_AppRequires2FA_ValidDeviceTrust_SkipsChallengeAndReturnsJwt()
    {
        var rawToken = "trusted-device-raw-token";
        var hash     = HashString(rawToken);

        var trust = new DeviceTrust
        {
            Id           = Guid.NewGuid(),
            FamilyUserId = UserId,
            AppClientId  = AppClient,
            TokenHash    = hash,
            ExpiresAt    = DateTime.UtcNow.AddDays(80)
        };

        var (svc, data, _) = Build(user: ActiveUser(), app: AppWith2FA(), access: ActiveAccess());
        data.Setup(d => d.GetDeviceTrustByHashAsync(hash)).ReturnsAsync(trust);

        var result = await svc.LoginAsync(ValidRequest(deviceTrustToken: rawToken), null);

        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.NotNull(result.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Login_AppRequires2FA_ExpiredDeviceTrust_IssuesChallenge()
    {
        // GetDeviceTrustByHashAsync returns null (expired / not found)
        var (svc, data, _) = Build(user: ActiveUser(), app: AppWith2FA(), access: ActiveAccess());
        data.Setup(d => d.GetDeviceTrustByHashAsync(It.IsAny<string>())).ReturnsAsync((DeviceTrust?)null);

        var result = await svc.LoginAsync(ValidRequest(deviceTrustToken: "old-token"), null);

        Assert.NotNull(result);
        Assert.True(result.RequiresTwoFactor);
    }

    [Fact]
    public async Task Login_AppDoesNotRequire2FA_ReturnsJwtDirectly()
    {
        var (svc, _, _) = Build(user: ActiveUser(), app: AppWith2FA(requires2Fa: false), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), null);

        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.NotNull(result.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    // ── VerifyTwoFactor path ─────────────────────────────────────────────────────

    private static (AuthAppService svc, Mock<IDataAccess> data, TwoFactorChallenge challenge)
        BuildForVerify(string otp, bool userActive = true, AppAccess? access = null)
    {
        var challenge = new TwoFactorChallenge
        {
            Id              = Guid.NewGuid(),
            FamilyUserId    = UserId,
            RegisteredAppId = AppId,
            ChallengeToken  = "challenge-abc",
            OtpHash         = HashString(otp),
            ExpiresAt       = DateTime.UtcNow.AddMinutes(10),
            User            = new FamilyUser
            {
                Id           = UserId,
                FirstName    = "Jane",
                LastName     = "Doe",
                Email        = Email,
                PasswordHash = PasswordHash,
                Role         = FamilyRoles.User,
                IsActive     = userActive,
                IsWard       = false
            },
            App = AppWith2FA()
        };

        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();

        data.Setup(d => d.GetTwoFactorChallengeAsync("challenge-abc")).ReturnsAsync(challenge);
        data.Setup(d => d.MarkChallengeUsedAsync(challenge.Id)).Returns(Task.CompletedTask);
        data.Setup(d => d.GetAppAccessAsync(UserId, AppId)).ReturnsAsync(access ?? ActiveAccess());
        data.Setup(d => d.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).ReturnsAsync((RefreshToken t) => t);
        data.Setup(d => d.UpdateLastAccessedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);
        data.Setup(d => d.CreateDeviceTrustAsync(It.IsAny<DeviceTrust>())).ReturnsAsync((DeviceTrust t) => t);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc        = new AuthAppService(data.Object, new JwtService(jwtOptions), email.Object,
                             jwtOptions, NullLogger<AuthAppService>.Instance);
        return (svc, data, challenge);
    }

    [Fact]
    public async Task VerifyTwoFactor_ValidOtp_ReturnsLoginResponse()
    {
        var otp = "123456";
        var (svc, _, _) = BuildForVerify(otp);

        var result = await svc.VerifyTwoFactorAsync(new VerifyTwoFactorRequest("challenge-abc", otp), null);

        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task VerifyTwoFactor_ValidOtp_MarksChallengUsed()
    {
        var otp = "654321";
        var (svc, data, challenge) = BuildForVerify(otp);

        await svc.VerifyTwoFactorAsync(new VerifyTwoFactorRequest("challenge-abc", otp), null);

        data.Verify(d => d.MarkChallengeUsedAsync(challenge.Id), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactor_ValidOtp_WritesAuditLog()
    {
        var otp = "111222";
        var (svc, data, _) = BuildForVerify(otp);

        await svc.VerifyTwoFactorAsync(new VerifyTwoFactorRequest("challenge-abc", otp), "5.6.7.8");

        data.Verify(d => d.WriteAuditLogAsync(It.Is<AuditLog>(log =>
            log.FamilyUserId == UserId &&
            log.Action       == "TwoFactorVerified" &&
            log.IpAddress    == "5.6.7.8")), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactor_InvalidOtp_ReturnsNull()
    {
        var (svc, _, _) = BuildForVerify("999999");

        var result = await svc.VerifyTwoFactorAsync(new VerifyTwoFactorRequest("challenge-abc", "000000"), null);

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyTwoFactor_ExpiredChallenge_ReturnsNull()
    {
        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();
        data.Setup(d => d.GetTwoFactorChallengeAsync(It.IsAny<string>())).ReturnsAsync((TwoFactorChallenge?)null);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc        = new AuthAppService(data.Object, new JwtService(jwtOptions), email.Object,
                             jwtOptions, NullLogger<AuthAppService>.Instance);

        var result = await svc.VerifyTwoFactorAsync(new VerifyTwoFactorRequest("stale-token", "123456"), null);

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithTrustDevice_ReturnsDeviceTrustToken()
    {
        var otp = "333444";
        var (svc, _, _) = BuildForVerify(otp);

        var result = await svc.VerifyTwoFactorAsync(
            new VerifyTwoFactorRequest("challenge-abc", otp, TrustDevice: true), null);

        Assert.NotNull(result);
        Assert.NotNull(result.DeviceTrustToken);
        Assert.False(string.IsNullOrWhiteSpace(result.DeviceTrustToken));
    }

    [Fact]
    public async Task VerifyTwoFactor_WithTrustDevice_StoresDeviceTrustInDb()
    {
        var otp = "555666";
        var (svc, data, _) = BuildForVerify(otp);

        await svc.VerifyTwoFactorAsync(
            new VerifyTwoFactorRequest("challenge-abc", otp, TrustDevice: true), null);

        data.Verify(d => d.CreateDeviceTrustAsync(It.Is<DeviceTrust>(t =>
            t.FamilyUserId == UserId &&
            t.AppClientId  == AppClient &&
            !string.IsNullOrEmpty(t.TokenHash))), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithoutTrustDevice_NoDeviceTrustToken()
    {
        var otp = "777888";
        var (svc, data, _) = BuildForVerify(otp);

        var result = await svc.VerifyTwoFactorAsync(
            new VerifyTwoFactorRequest("challenge-abc", otp, TrustDevice: false), null);

        Assert.NotNull(result);
        Assert.Null(result.DeviceTrustToken);
        data.Verify(d => d.CreateDeviceTrustAsync(It.IsAny<DeviceTrust>()), Times.Never);
    }
}
