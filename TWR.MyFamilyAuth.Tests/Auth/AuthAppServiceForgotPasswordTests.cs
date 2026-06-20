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

public class AuthAppServiceForgotPasswordTests
{
    private static readonly Guid   UserId = Guid.NewGuid();
    private static readonly Guid   TokenId = Guid.NewGuid();
    private const           string Email   = "test@example.com";

    private static FamilyUser ActiveUser() => new()
    {
        Id           = UserId,
        FirstName    = "Jane",
        LastName     = "Doe",
        Email        = Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
        Role         = FamilyRoles.User,
        IsActive     = true
    };

    private static JwtSettings TestJwtSettings() => new()
    {
        Issuer                 = "test-issuer",
        Audience               = "test-audience",
        Secret                 = "test-secret-key-must-be-at-least-32-chars-long!",
        ExpiryMinutes          = 60,
        RefreshTokenExpiryDays = 30
    };

    private static (AuthAppService svc, Mock<IDataAccess> dataMock, Mock<IEmailService> emailMock) Build(
        FamilyUser? user = null, PasswordResetToken? resetToken = null)
    {
        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();

        data.Setup(d => d.GetUserByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        data.Setup(d => d.CreatePasswordResetTokenAsync(It.IsAny<PasswordResetToken>()))
            .ReturnsAsync((PasswordResetToken t) => { t.Id = TokenId; return t; });

        data.Setup(d => d.GetPasswordResetTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(resetToken);

        data.Setup(d => d.UpdatePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        data.Setup(d => d.MarkPasswordResetTokenUsedAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        email.Setup(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        var jwtOptions = Options.Create(TestJwtSettings());
        var jwt        = new JwtService(jwtOptions);
        var logger     = NullLogger<AuthAppService>.Instance;

        var svc = new AuthAppService(data.Object, jwt, email.Object, jwtOptions, logger);
        return (svc, data, email);
    }

    // ── ForgotPassword tests ────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsTrueWithoutSendingEmail()
    {
        var (svc, _, email) = Build(user: null);
        var result = await svc.ForgotPasswordAsync("nobody@example.com");
        Assert.True(result);
        email.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_InactiveUser_ReturnsTrueWithoutSendingEmail()
    {
        var inactiveUser = ActiveUser();
        inactiveUser.IsActive = false;
        var (svc, _, email) = Build(user: inactiveUser);
        var result = await svc.ForgotPasswordAsync(Email);
        Assert.True(result);
        email.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_ValidUser_ReturnsTrueAndSendsEmail()
    {
        var (svc, _, email) = Build(user: ActiveUser());
        var result = await svc.ForgotPasswordAsync(Email);
        Assert.True(result);
        email.Verify(e => e.SendPasswordResetAsync(Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_ValidUser_StoresResetToken()
    {
        var (svc, data, _) = Build(user: ActiveUser());
        await svc.ForgotPasswordAsync(Email);
        data.Verify(d => d.CreatePasswordResetTokenAsync(It.Is<PasswordResetToken>(t =>
            t.FamilyUserId == UserId && !string.IsNullOrEmpty(t.Token))), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_ValidUser_EmailIsNormalized()
    {
        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();

        data.Setup(d => d.GetUserByEmailAsync("test@example.com")).ReturnsAsync(ActiveUser());
        data.Setup(d => d.CreatePasswordResetTokenAsync(It.IsAny<PasswordResetToken>()))
            .ReturnsAsync((PasswordResetToken t) => t);
        email.Setup(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new AuthAppService(data.Object, new JwtService(jwtOptions), email.Object, jwtOptions,
            NullLogger<AuthAppService>.Instance);

        await svc.ForgotPasswordAsync("  TEST@EXAMPLE.COM  ");

        data.Verify(d => d.GetUserByEmailAsync("test@example.com"), Times.Once);
    }

    // ── ResetPassword tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsTrueAndUpdatesPassword()
    {
        var resetToken = new PasswordResetToken { Id = TokenId, FamilyUserId = UserId, Token = "123456", ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var (svc, _, _) = Build(user: ActiveUser(), resetToken: resetToken);
        var result = await svc.ResetPasswordAsync(new ResetPasswordRequest("123456", "NewP@ssw0rd!"));
        Assert.True(result);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsFalse()
    {
        var (svc, _, _) = Build(user: ActiveUser(), resetToken: null);
        var result = await svc.ResetPasswordAsync(new ResetPasswordRequest("bad-token", "NewP@ssw0rd!"));
        Assert.False(result);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_MarksTokenUsed()
    {
        var resetToken = new PasswordResetToken { Id = TokenId, FamilyUserId = UserId, Token = "123456", ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var (svc, data, _) = Build(user: ActiveUser(), resetToken: resetToken);
        await svc.ResetPasswordAsync(new ResetPasswordRequest("123456", "NewP@ssw0rd!"));
        data.Verify(d => d.MarkPasswordResetTokenUsedAsync(TokenId), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_WritesAuditLog()
    {
        var resetToken = new PasswordResetToken { Id = TokenId, FamilyUserId = UserId, Token = "123456", ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var (svc, data, _) = Build(user: ActiveUser(), resetToken: resetToken);
        await svc.ResetPasswordAsync(new ResetPasswordRequest("123456", "NewP@ssw0rd!"));
        data.Verify(d => d.WriteAuditLogAsync(It.Is<AuditLog>(l =>
            l.FamilyUserId == UserId && l.Action == "PasswordReset")), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_HashesPasswordCorrectly()
    {
        const string newPassword = "NewP@ssw0rd!";
        string? capturedHash    = null;

        var data  = new Mock<IDataAccess>();
        var email = new Mock<IEmailService>();
        var resetToken = new PasswordResetToken { Id = TokenId, FamilyUserId = UserId, Token = "123456", ExpiresAt = DateTime.UtcNow.AddMinutes(10) };

        data.Setup(d => d.GetPasswordResetTokenAsync("123456")).ReturnsAsync(resetToken);
        data.Setup(d => d.UpdatePasswordAsync(UserId, It.IsAny<string>()))
            .Callback<Guid, string>((_, h) => capturedHash = h)
            .Returns(Task.CompletedTask);
        data.Setup(d => d.MarkPasswordResetTokenUsedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new AuthAppService(data.Object, new JwtService(jwtOptions), email.Object, jwtOptions,
            NullLogger<AuthAppService>.Instance);

        await svc.ResetPasswordAsync(new ResetPasswordRequest("123456", newPassword));

        Assert.NotNull(capturedHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, capturedHash));
    }
}
