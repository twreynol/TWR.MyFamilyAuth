using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.Tests.Auth;

/// <summary>
/// Unit tests for WebAuthnAppService. IFido2 is mocked rather than exercised with real captured
/// attestation/assertion byte fixtures — the cryptographic verification itself is Fido2NetLib's
/// own tested code; what's ours to regress-test is the RP/origin resolution, challenge lifecycle,
/// and DAL wiring around it.
/// </summary>
public class WebAuthnTests
{
    private static readonly Guid   UserId    = Guid.NewGuid();
    private static readonly Guid   AppId     = Guid.NewGuid();
    private const           string Email     = "webauthn@example.com";
    private const           string AppClient = "myfinances";
    private const           string Origin    = "https://localhost:7237";
    private const           string OtherOrigin = "https://myfamilyfinances-mobile-v2.fly.dev";

    private static FamilyUser ActiveUser() => new()
    {
        Id        = UserId,
        FirstName = "Jane",
        LastName  = "Doe",
        Email     = Email,
        Role      = FamilyRoles.User,
        IsActive  = true,
        IsWard    = false
    };

    private static RegisteredApp AppWithOrigins(params string[] origins) => new()
    {
        Id             = AppId,
        Name           = "MyFinances",
        ClientId       = AppClient,
        IsActive       = true,
        AllowedOrigins = System.Text.Json.JsonSerializer.Serialize(origins)
    };

    private static AppAccess ActiveAccess() => new()
    {
        Id              = Guid.NewGuid(),
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

    private static (WebAuthnAppService svc, Mock<IDataAccess> data, Mock<IFido2> fido2)
        Build(RegisteredApp? app = null)
    {
        var data  = new Mock<IDataAccess>();
        var fido2 = new Mock<IFido2>();
        var fido2Factory = new Mock<IFido2Factory>();
        fido2Factory.Setup(f => f.Create(It.IsAny<Fido2Configuration>())).Returns(fido2.Object);

        data.Setup(d => d.GetRegisteredAppByClientIdAsync(AppClient)).ReturnsAsync(app ?? AppWithOrigins(Origin));
        data.Setup(d => d.GetUserByIdAsync(UserId)).ReturnsAsync(ActiveUser());
        data.Setup(d => d.GetUserByEmailAsync(Email)).ReturnsAsync(ActiveUser());
        data.Setup(d => d.GetAppAccessAsync(UserId, AppId)).ReturnsAsync(ActiveAccess());
        data.Setup(d => d.GetPermissionsForUserAsync(UserId)).ReturnsAsync([]);
        data.Setup(d => d.UpdateLastAccessedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.WriteAuditLogAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);
        data.Setup(d => d.StoreRefreshTokenAsync(It.IsAny<RefreshToken>())).ReturnsAsync((RefreshToken t) => t);
        data.Setup(d => d.CreateWebAuthnChallengeAsync(It.IsAny<WebAuthnChallenge>()))
            .ReturnsAsync((WebAuthnChallenge c) => { c.Id = Guid.NewGuid(); return c; });
        data.Setup(d => d.MarkWebAuthnChallengeUsedAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        data.Setup(d => d.CreateWebAuthnCredentialAsync(It.IsAny<WebAuthnCredential>()))
            .ReturnsAsync((WebAuthnCredential c) => { c.Id = Guid.NewGuid(); c.CreatedAt = DateTime.UtcNow; return c; });
        data.Setup(d => d.UpdateWebAuthnCredentialSignCountAsync(It.IsAny<Guid>(), It.IsAny<long>())).Returns(Task.CompletedTask);
        data.Setup(d => d.GetWebAuthnCredentialsByUserAndRpIdAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync([]);

        fido2.Setup(f => f.RequestNewCredential(It.IsAny<RequestNewCredentialParams>()))
            .Returns(FakeCredentialCreateOptions());
        fido2.Setup(f => f.GetAssertionOptions(It.IsAny<GetAssertionOptionsParams>()))
            .Returns(new AssertionOptions { Challenge = [4, 5, 6] });

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new WebAuthnAppService(data.Object, fido2Factory.Object, new JwtService(jwtOptions),
            jwtOptions, NullLogger<WebAuthnAppService>.Instance);

        return (svc, data, fido2);
    }

    // ── Registration: register-options ──────────────────────────────────────────

    [Fact]
    public async Task GetRegisterOptions_ValidOriginAndUser_ReturnsChallengeAndPersistsIt()
    {
        var (svc, data, _) = Build();

        var result = await svc.GetRegisterOptionsAsync(UserId, AppClient, Origin);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.ChallengeToken));
        data.Verify(d => d.CreateWebAuthnChallengeAsync(It.Is<WebAuthnChallenge>(c =>
            c.FamilyUserId == UserId && c.ChallengeKind == "Registration")), Times.Once);
    }

    [Fact]
    public async Task GetRegisterOptions_OriginNotInAllowedList_ReturnsNull()
    {
        var (svc, _, _) = Build(app: AppWithOrigins(Origin));

        var result = await svc.GetRegisterOptionsAsync(UserId, AppClient, OtherOrigin);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRegisterOptions_UnknownApp_ReturnsNull()
    {
        var data  = new Mock<IDataAccess>();
        var fido2Factory = new Mock<IFido2Factory>();
        data.Setup(d => d.GetRegisteredAppByClientIdAsync(It.IsAny<string>())).ReturnsAsync((RegisteredApp?)null);

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new WebAuthnAppService(data.Object, fido2Factory.Object, new JwtService(jwtOptions),
            jwtOptions, NullLogger<WebAuthnAppService>.Instance);

        var result = await svc.GetRegisterOptionsAsync(UserId, AppClient, Origin);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRegisterOptions_ExcludesExistingCredentialsForSameRp()
    {
        var (svc, data, fido2) = Build();
        var existingCredId = Base64Url.Encode([9, 9, 9]);
        data.Setup(d => d.GetWebAuthnCredentialsByUserAndRpIdAsync(UserId, "localhost"))
            .ReturnsAsync([new WebAuthnCredential { CredentialId = existingCredId, UserHandle = Base64Url.Encode([1, 1, 1]) }]);

        await svc.GetRegisterOptionsAsync(UserId, AppClient, Origin);

        fido2.Verify(f => f.RequestNewCredential(It.Is<RequestNewCredentialParams>(p =>
            p.ExcludeCredentials!.Count == 1)), Times.Once);
    }

    // ── Registration: register-complete ─────────────────────────────────────────

    private static WebAuthnChallenge RegistrationChallenge(string optionsJson = "{}") => new()
    {
        Id              = Guid.NewGuid(),
        FamilyUserId    = UserId,
        RegisteredAppId = AppId,
        RpId            = "localhost",
        ChallengeToken  = "reg-challenge",
        ChallengeKind   = "Registration",
        OptionsJson     = System.Text.Json.JsonSerializer.Serialize(FakeCredentialCreateOptions()),
        ExpiresAt       = DateTime.UtcNow.AddMinutes(2),
        App             = AppWithOrigins(Origin)
    };

    private static CredentialCreateOptions FakeCredentialCreateOptions() => new()
    {
        Challenge        = [1, 2, 3],
        Rp               = new PublicKeyCredentialRpEntity(id: "localhost", name: "MyFinances", icon: null),
        User             = new Fido2User { Id = [1, 1, 1], Name = Email, DisplayName = "Jane Doe" },
        PubKeyCredParams = [PubKeyCredParam.ES256]
    };

    private static string FakeAttestationJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        id = "abc",
        rawId = Base64Url.Encode([1, 2, 3]),
        type = "public-key",
        response = new
        {
            attestationObject = Base64Url.Encode([1]),
            clientDataJSON    = Base64Url.Encode([2])
        }
    });

    [Fact]
    public async Task CompleteRegister_ValidAttestation_PersistsCredentialAndMarksChallengeUsed()
    {
        var (svc, data, fido2) = Build();
        var challenge = RegistrationChallenge();
        data.Setup(d => d.GetWebAuthnChallengeAsync("reg-challenge")).ReturnsAsync(challenge);

        fido2.Setup(f => f.MakeNewCredentialAsync(It.IsAny<MakeNewCredentialParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisteredPublicKeyCredential
            {
                Id        = [7, 7, 7],
                PublicKey = [8, 8, 8],
                SignCount = 0,
                User      = new Fido2User { Id = [1, 1, 1], Name = Email, DisplayName = "Jane Doe" },
                Transports = [AuthenticatorTransport.Internal]
            });

        var result = await svc.CompleteRegisterAsync(UserId, AppClient, Origin,
            new RegisterCompleteRequest("reg-challenge", FakeAttestationJson()));

        Assert.NotNull(result);
        data.Verify(d => d.MarkWebAuthnChallengeUsedAsync(challenge.Id), Times.Once);
        data.Verify(d => d.CreateWebAuthnCredentialAsync(It.Is<WebAuthnCredential>(c =>
            c.FamilyUserId == UserId && c.RpId == "localhost")), Times.Once);
    }

    [Fact]
    public async Task CompleteRegister_WrongChallengeKind_ReturnsNull()
    {
        var (svc, data, _) = Build();
        var challenge = RegistrationChallenge();
        challenge.ChallengeKind = "Assertion";
        data.Setup(d => d.GetWebAuthnChallengeAsync("reg-challenge")).ReturnsAsync(challenge);

        var result = await svc.CompleteRegisterAsync(UserId, AppClient, Origin,
            new RegisterCompleteRequest("reg-challenge", FakeAttestationJson()));

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteRegister_Fido2VerificationThrows_ReturnsNull()
    {
        var (svc, data, fido2) = Build();
        var challenge = RegistrationChallenge();
        data.Setup(d => d.GetWebAuthnChallengeAsync("reg-challenge")).ReturnsAsync(challenge);
        fido2.Setup(f => f.MakeNewCredentialAsync(It.IsAny<MakeNewCredentialParams>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Fido2VerificationException("bad attestation"));

        var result = await svc.CompleteRegisterAsync(UserId, AppClient, Origin,
            new RegisterCompleteRequest("reg-challenge", FakeAttestationJson()));

        Assert.Null(result);
        data.Verify(d => d.CreateWebAuthnCredentialAsync(It.IsAny<WebAuthnCredential>()), Times.Never);
    }

    // ── Login: login-options ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLoginOptions_UnknownEmail_ReturnsResponseWithoutPersistingChallenge()
    {
        var data  = new Mock<IDataAccess>();
        var fido2 = new Mock<IFido2>();
        var fido2Factory = new Mock<IFido2Factory>();
        fido2Factory.Setup(f => f.Create(It.IsAny<Fido2Configuration>())).Returns(fido2.Object);
        data.Setup(d => d.GetRegisteredAppByClientIdAsync(AppClient)).ReturnsAsync(AppWithOrigins(Origin));
        data.Setup(d => d.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((FamilyUser?)null);
        fido2.Setup(f => f.GetAssertionOptions(It.IsAny<GetAssertionOptionsParams>()))
            .Returns(new AssertionOptions { Challenge = [4, 5, 6] });

        var jwtOptions = Options.Create(TestJwtSettings());
        var svc = new WebAuthnAppService(data.Object, fido2Factory.Object, new JwtService(jwtOptions),
            jwtOptions, NullLogger<WebAuthnAppService>.Instance);

        var result = await svc.GetLoginOptionsAsync(new WebAuthnLoginOptionsRequest("nobody@example.com", AppClient), Origin);

        Assert.NotNull(result);
        data.Verify(d => d.CreateWebAuthnChallengeAsync(It.IsAny<WebAuthnChallenge>()), Times.Never);
    }

    [Fact]
    public async Task GetLoginOptions_KnownEmailWithCredentials_PersistsChallenge()
    {
        var (svc, data, _) = Build();
        data.Setup(d => d.GetWebAuthnCredentialsByUserAndRpIdAsync(UserId, "localhost"))
            .ReturnsAsync([new WebAuthnCredential { CredentialId = Base64Url.Encode([1]), UserHandle = Base64Url.Encode([2]) }]);

        var result = await svc.GetLoginOptionsAsync(new WebAuthnLoginOptionsRequest(Email, AppClient), Origin);

        Assert.NotNull(result);
        data.Verify(d => d.CreateWebAuthnChallengeAsync(It.Is<WebAuthnChallenge>(c =>
            c.FamilyUserId == UserId && c.ChallengeKind == "Assertion")), Times.Once);
    }

    // ── Login: login-complete ───────────────────────────────────────────────────

    private static WebAuthnChallenge AssertionChallenge(RegisteredApp app) => new()
    {
        Id              = Guid.NewGuid(),
        FamilyUserId    = UserId,
        RegisteredAppId = AppId,
        RpId            = "localhost",
        ChallengeToken  = "login-challenge",
        ChallengeKind   = "Assertion",
        OptionsJson     = System.Text.Json.JsonSerializer.Serialize(new AssertionOptions { Challenge = [4, 5, 6] }),
        ExpiresAt       = DateTime.UtcNow.AddMinutes(2),
        App             = app
    };

    private static string FakeAssertionJson(byte[] rawId) => System.Text.Json.JsonSerializer.Serialize(new
    {
        id = Base64Url.Encode(rawId),
        rawId = Base64Url.Encode(rawId),
        type = "public-key",
        response = new
        {
            authenticatorData = Base64Url.Encode([1]),
            signature         = Base64Url.Encode([2]),
            clientDataJSON    = Base64Url.Encode([3]),
            userHandle        = Base64Url.Encode([9])
        }
    });

    [Fact]
    public async Task CompleteLogin_OriginMismatch_ReturnsNull_MultiOriginRegressionCheck()
    {
        var (svc, data, _) = Build();
        var app = AppWithOrigins(Origin); // credential/challenge bound to "localhost" RP only
        data.Setup(d => d.GetWebAuthnChallengeAsync("login-challenge")).ReturnsAsync(AssertionChallenge(app));

        // Asserting from a different origin than the one the challenge/RP was bound to must fail —
        // this is the actual regression test for the multi-tenant RP-ID design.
        var result = await svc.CompleteLoginAsync(
            new WebAuthnLoginCompleteRequest("login-challenge", FakeAssertionJson([1, 2, 3])),
            OtherOrigin, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteLogin_ValidAssertion_ReturnsJwtAndUpdatesSignCount()
    {
        var (svc, data, fido2) = Build();
        var app = AppWithOrigins(Origin);
        var challenge = AssertionChallenge(app);
        data.Setup(d => d.GetWebAuthnChallengeAsync("login-challenge")).ReturnsAsync(challenge);

        var rawId = new byte[] { 1, 2, 3 };
        var storedCredential = new WebAuthnCredential
        {
            Id           = Guid.NewGuid(),
            FamilyUserId = UserId,
            RpId         = "localhost",
            CredentialId = Base64Url.Encode(rawId),
            PublicKey    = Convert.ToBase64String([8, 8, 8]),
            SignCount    = 5,
            UserHandle   = Base64Url.Encode([9]),
            User         = ActiveUser()
        };
        data.Setup(d => d.GetWebAuthnCredentialByCredentialIdAsync(Base64Url.Encode(rawId)))
            .ReturnsAsync(storedCredential);

        fido2.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyAssertionResult { CredentialId = rawId, SignCount = 6 });

        var result = await svc.CompleteLoginAsync(
            new WebAuthnLoginCompleteRequest("login-challenge", FakeAssertionJson(rawId)),
            Origin, "1.2.3.4");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        data.Verify(d => d.UpdateWebAuthnCredentialSignCountAsync(storedCredential.Id, 6), Times.Once);
        data.Verify(d => d.MarkWebAuthnChallengeUsedAsync(challenge.Id), Times.Once);
    }

    [Fact]
    public async Task CompleteLogin_Fido2VerificationThrows_ReturnsNull()
    {
        var (svc, data, fido2) = Build();
        var app = AppWithOrigins(Origin);
        var challenge = AssertionChallenge(app);
        data.Setup(d => d.GetWebAuthnChallengeAsync("login-challenge")).ReturnsAsync(challenge);

        var rawId = new byte[] { 1, 2, 3 };
        data.Setup(d => d.GetWebAuthnCredentialByCredentialIdAsync(Base64Url.Encode(rawId)))
            .ReturnsAsync(new WebAuthnCredential
            {
                Id = Guid.NewGuid(), RpId = "localhost", CredentialId = Base64Url.Encode(rawId),
                PublicKey = Convert.ToBase64String([8]), SignCount = 5, UserHandle = Base64Url.Encode([9]),
                User = ActiveUser()
            });

        fido2.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Fido2VerificationException("replayed sign count"));

        var result = await svc.CompleteLoginAsync(
            new WebAuthnLoginCompleteRequest("login-challenge", FakeAssertionJson(rawId)),
            Origin, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteLogin_UnknownChallengeToken_ReturnsNull()
    {
        var (svc, data, _) = Build();
        data.Setup(d => d.GetWebAuthnChallengeAsync(It.IsAny<string>())).ReturnsAsync((WebAuthnChallenge?)null);

        var result = await svc.CompleteLoginAsync(
            new WebAuthnLoginCompleteRequest("stale-token", FakeAssertionJson([1])), Origin, null);

        Assert.Null(result);
    }

    // ── List / delete passkeys ───────────────────────────────────────────────────

    [Fact]
    public async Task ListPasskeys_ValidOrigin_ReturnsCredentialsForRp()
    {
        var (svc, data, _) = Build();
        var cred = new WebAuthnCredential
        {
            Id = Guid.NewGuid(), DeviceLabel = "My Laptop",
            CreatedAt = DateTime.UtcNow.AddDays(-1), LastUsedAt = DateTime.UtcNow
        };
        data.Setup(d => d.GetWebAuthnCredentialsByUserAndRpIdAsync(UserId, "localhost"))
            .ReturnsAsync([cred]);

        var result = await svc.ListPasskeysAsync(UserId, AppClient, Origin);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("My Laptop", result[0].DeviceLabel);
    }

    [Fact]
    public async Task ListPasskeys_OriginNotInAllowedList_ReturnsNull()
    {
        var (svc, _, _) = Build(app: AppWithOrigins(Origin));

        var result = await svc.ListPasskeysAsync(UserId, AppClient, OtherOrigin);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeletePasskey_OwnedByCaller_DelegatesToDataAccessAndReturnsTrue()
    {
        var (svc, data, _) = Build();
        var credId = Guid.NewGuid();
        data.Setup(d => d.DeleteWebAuthnCredentialAsync(credId, UserId)).ReturnsAsync(true);

        var result = await svc.DeletePasskeyAsync(UserId, credId);

        Assert.True(result);
        data.Verify(d => d.DeleteWebAuthnCredentialAsync(credId, UserId), Times.Once);
    }

    [Fact]
    public async Task DeletePasskey_NotOwnedByCaller_ReturnsFalse()
    {
        var (svc, data, _) = Build();
        var credId = Guid.NewGuid();
        data.Setup(d => d.DeleteWebAuthnCredentialAsync(credId, UserId)).ReturnsAsync(false);

        var result = await svc.DeletePasskeyAsync(UserId, credId);

        Assert.False(result);
    }
}
