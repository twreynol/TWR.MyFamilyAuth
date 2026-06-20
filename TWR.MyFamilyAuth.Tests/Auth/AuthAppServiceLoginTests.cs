using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.Tests.Auth;

/// <summary>
/// Unit tests for AuthAppService.LoginAsync.
/// The DAL is mocked; JwtService uses a real instance (pure deterministic logic, no I/O).
/// </summary>
public class AuthAppServiceLoginTests
{
    // ── Fixed test data ─────────────────────────────────────────────────────────

    private static readonly Guid   UserId    = Guid.NewGuid();
    private static readonly Guid   AppId     = Guid.NewGuid();
    private static readonly Guid   AccessId  = Guid.NewGuid();
    private const           string Email     = "test@example.com";
    private const           string Password  = "P@ssw0rd!";
    private const           string AppClient = "myfinances";

    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static FamilyUser ActiveUser(bool isWard = false, bool isActive = true) => new()
    {
        Id           = UserId,
        FirstName    = "Jane",
        LastName     = "Doe",
        Email        = Email,
        PasswordHash = PasswordHash,
        Role         = FamilyRoles.User,
        IsActive     = isActive,
        IsWard       = isWard
    };

    private static RegisteredApp ActiveApp() => new()
    {
        Id       = AppId,
        Name     = "MyFinances",
        ClientId = AppClient,
        IsActive = true
    };

    private static AppAccess ActiveAccess(string? appRole = "User") => new()
    {
        Id              = AccessId,
        FamilyUserId    = UserId,
        RegisteredAppId = AppId,
        AppRole         = appRole,
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

    private static (AuthAppService svc, Mock<IDataAccess> dataMock) Build(
        FamilyUser?     user        = null,
        RegisteredApp?  app         = null,
        AppAccess?      access      = null)
    {
        var data = new Mock<IDataAccess>();

        data.Setup(d => d.GetUserByEmailAsync(Email))
            .ReturnsAsync(user);

        data.Setup(d => d.GetRegisteredAppByClientIdAsync(AppClient))
            .ReturnsAsync(app);

        data.Setup(d => d.GetAppAccessAsync(UserId, AppId))
            .ReturnsAsync(access);

        // Side-effect methods — allow calls, return harmlessly
        data.Setup(d => d.UpdateUserAsync(It.IsAny<FamilyUser>()))
            .ReturnsAsync((FamilyUser u) => u);
        data.Setup(d => d.UpdateLastAccessedAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);
        data.Setup(d => d.StoreRefreshTokenAsync(It.IsAny<RefreshToken>()))
            .ReturnsAsync((RefreshToken t) => t);

        var jwtOptions  = Options.Create(TestJwtSettings());
        var jwt         = new JwtService(jwtOptions);
        var emailMock   = new Mock<IEmailService>();
        var logger      = NullLogger<AuthAppService>.Instance;

        var svc = new AuthAppService(data.Object, jwt, emailMock.Object, jwtOptions, logger);
        return (svc, data);
    }

    private static LoginRequest ValidRequest(bool rememberMe = false) =>
        new(Email, Password, AppClient, RememberMe: rememberMe);

    // ── Happy-path tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentialsAndAccess_ReturnsLoginResponse()
    {
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.NotNull(result);
        Assert.Equal(UserId,        result.UserId);
        Assert.Equal(Email,         result.Email);
        Assert.Equal(FamilyRoles.User, result.Role);
        Assert.False(result.MustChangePassword);
    }

    [Fact]
    public async Task Login_ValidCredentials_TokenIsNonEmpty()
    {
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Login_WithoutRememberMe_RefreshTokenIsNull()
    {
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(rememberMe: false), ipAddress: null);

        Assert.NotNull(result);
        Assert.Null(result.RefreshToken);
    }

    [Fact]
    public async Task Login_WithRememberMe_RefreshTokenIsReturned()
    {
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(rememberMe: true), ipAddress: null);

        Assert.NotNull(result);
        Assert.NotNull(result.RefreshToken);
    }

    [Fact]
    public async Task Login_WithRememberMe_RefreshTokenIsStoredInDal()
    {
        var (svc, data) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        await svc.LoginAsync(ValidRequest(rememberMe: true), ipAddress: null);

        data.Verify(d => d.StoreRefreshTokenAsync(It.Is<RefreshToken>(t =>
            t.FamilyUserId == UserId &&
            !string.IsNullOrEmpty(t.TokenHash) &&
            t.AppClientId  == AppClient)),
            Times.Once);
    }

    [Fact]
    public async Task Login_Success_AuditLogIsWritten()
    {
        var (svc, data) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        await svc.LoginAsync(ValidRequest(), ipAddress: "1.2.3.4");

        data.Verify(d => d.WriteAuditLogAsync(It.Is<AuditLog>(log =>
            log.FamilyUserId == UserId &&
            log.Action       == "Login" &&
            log.IpAddress    == "1.2.3.4")),
            Times.Once);
    }

    [Fact]
    public async Task Login_Success_LastAccessedIsUpdated()
    {
        var (svc, data) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        await svc.LoginAsync(ValidRequest(), ipAddress: null);

        data.Verify(d => d.UpdateLastAccessedAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task Login_AppRoleInAccess_AppRoleAppearsInToken()
    {
        var access = ActiveAccess(appRole: "Owner");
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: access);

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        // Validate the token and inspect claims
        Assert.NotNull(result);
        var jwt     = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed  = jwt.ReadJwtToken(result.Token);
        var appRole = parsed.Claims.FirstOrDefault(c => c.Type == "app_role")?.Value;
        Assert.Equal("Owner", appRole);
    }

    // ── Rejection tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UnknownEmail_ReturnsNull()
    {
        var (svc, _) = Build(user: null, app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(
            new LoginRequest(Email, "wrong-password", AppClient), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_DeactivatedUser_ReturnsNull()
    {
        var (svc, _) = Build(user: ActiveUser(isActive: false), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_WardUser_ReturnsNull()
    {
        // Wards cannot log in — their guardian manages their data
        var (svc, _) = Build(user: ActiveUser(isWard: true), app: ActiveApp(), access: ActiveAccess());

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_UnknownApp_ReturnsNull()
    {
        var (svc, _) = Build(user: ActiveUser(), app: null, access: null);

        var result = await svc.LoginAsync(
            new LoginRequest(Email, Password, "nonexistent-app"), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_NoAppAccess_ReturnsNull()
    {
        // User exists and app exists, but no AppAccess row
        var (svc, _) = Build(user: ActiveUser(), app: ActiveApp(), access: null);

        var result = await svc.LoginAsync(ValidRequest(), ipAddress: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_NoAppAccess_AuditLogIsNotWritten()
    {
        // Denied logins should not leak information via audit logs visible to the user
        var (svc, data) = Build(user: ActiveUser(), app: ActiveApp(), access: null);

        await svc.LoginAsync(ValidRequest(), ipAddress: null);

        data.Verify(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>()), Times.Never);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var data = new Mock<IDataAccess>();
        // DAL always returns the user regardless of email casing (it normalises in DB)
        data.Setup(d => d.GetUserByEmailAsync("test@example.com")).ReturnsAsync(ActiveUser());
        data.Setup(d => d.GetRegisteredAppByClientIdAsync(AppClient)).ReturnsAsync(ActiveApp());
        data.Setup(d => d.GetAppAccessAsync(UserId, AppId)).ReturnsAsync(ActiveAccess());
        data.Setup(d => d.UpdateLastAccessedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new AuthAppService(data.Object, new JwtService(jwtOptions),
            new Mock<IEmailService>().Object, jwtOptions, NullLogger<AuthAppService>.Instance);

        // Service trims and lowercases before calling DAL
        var result = await svc.LoginAsync(
            new LoginRequest("  TEST@EXAMPLE.COM  ", Password, AppClient), ipAddress: null);

        Assert.NotNull(result);
        data.Verify(d => d.GetUserByEmailAsync("test@example.com"), Times.Once);
    }
}
