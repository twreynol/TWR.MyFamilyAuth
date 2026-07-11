using System.Security.Cryptography;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
using TWR.MyFamilyAuth.DAL.Entities;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API.AppServices;

public class WebAuthnAppService : IWebAuthnAppService
{
    private const string RegistrationChallengeKind = "Registration";
    private const string AssertionChallengeKind     = "Assertion";
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);

    private readonly IDataAccess    _data;
    private readonly IFido2Factory  _fido2Factory;
    private readonly IJwtService    _jwt;
    private readonly JwtSettings    _jwtSettings;
    private readonly ILogger<WebAuthnAppService> _logger;

    public WebAuthnAppService(IDataAccess data, IFido2Factory fido2Factory, IJwtService jwt,
        IOptions<JwtSettings> jwtSettings, ILogger<WebAuthnAppService> logger)
    {
        _data         = data;
        _fido2Factory = fido2Factory;
        _jwt          = jwt;
        _jwtSettings  = jwtSettings.Value;
        _logger       = logger;
    }

    private sealed record RpContext(RegisteredApp App, string RpId, IFido2 Fido2);

    // The only security gate for which RP a request belongs to: the Origin header must match
    // one of the app's registered origins exactly. CORS already enforces the browser only sends
    // these origins for real cross-origin calls from each consuming app's own frontend.
    private static bool TryValidateOrigin(RegisteredApp app, string? origin, out string rpId)
    {
        rpId = string.Empty;
        if (string.IsNullOrWhiteSpace(origin) || !app.IsActive) return false;

        List<string> allowed;
        try { allowed = JsonSerializer.Deserialize<List<string>>(app.AllowedOrigins) ?? []; }
        catch { allowed = []; }

        if (!allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

        rpId = uri.Host;
        return true;
    }

    private async Task<RpContext?> ResolveRpContextAsync(string appClientId, string? origin)
    {
        var app = await _data.GetRegisteredAppByClientIdAsync(appClientId);
        if (app is null || !TryValidateOrigin(app, origin, out var rpId)) return null;

        var config = new Fido2Configuration
        {
            ServerDomain = rpId,
            ServerName   = app.Name,
            Origins      = new HashSet<string> { origin! }
        };
        return new RpContext(app, rpId, _fido2Factory.Create(config));
    }

    public async Task<RegisterOptionsResponse?> GetRegisterOptionsAsync(Guid familyUserId, string appClientId, string? origin)
    {
        var rp = await ResolveRpContextAsync(appClientId, origin);
        if (rp is null) return null;

        var user = await _data.GetUserByIdAsync(familyUserId);
        if (user is null || !user.IsActive) return null;

        var existing = await _data.GetWebAuthnCredentialsByUserAndRpIdAsync(familyUserId, rp.RpId);

        var excludeCredentials = existing
            .Select(c => new PublicKeyCredentialDescriptor(Base64Url.Decode(c.CredentialId)))
            .ToList();

        // Reuse the same UserHandle across all of a user's credentials for one RP — it's the
        // stable identifier an authenticator associates with "this account," not a per-credential value.
        var userHandle = existing.Count > 0
            ? Base64Url.Decode(existing[0].UserHandle)
            : RandomNumberGenerator.GetBytes(32);

        var fido2User = new Fido2User
        {
            Id          = userHandle,
            Name        = user.Email,
            DisplayName = user.FullName
        };

        var options = rp.Fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User                   = fido2User,
            ExcludeCredentials      = excludeCredentials,
            AttestationPreference   = AttestationConveyancePreference.None,
            AuthenticatorSelection  = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                UserVerification        = UserVerificationRequirement.Required,
                ResidentKey             = ResidentKeyRequirement.Discouraged
            }
        });

        var optionsJson = JsonSerializer.Serialize(options);

        var challenge = await _data.CreateWebAuthnChallengeAsync(new WebAuthnChallenge
        {
            FamilyUserId    = familyUserId,
            RegisteredAppId = rp.App.Id,
            RpId            = rp.RpId,
            ChallengeToken  = Guid.NewGuid().ToString("N"),
            ChallengeKind   = RegistrationChallengeKind,
            OptionsJson     = optionsJson,
            ExpiresAt       = DateTime.UtcNow.Add(ChallengeLifetime)
        });

        return new RegisterOptionsResponse(challenge.ChallengeToken, optionsJson);
    }

    public async Task<RegisterCompleteResponse?> CompleteRegisterAsync(Guid familyUserId, string appClientId, string? origin, RegisterCompleteRequest request)
    {
        var rp = await ResolveRpContextAsync(appClientId, origin);
        if (rp is null) return null;

        var challenge = await _data.GetWebAuthnChallengeAsync(request.ChallengeToken);
        if (challenge is null ||
            challenge.ChallengeKind != RegistrationChallengeKind ||
            challenge.FamilyUserId != familyUserId ||
            challenge.RpId != rp.RpId)
            return null;

        await _data.MarkWebAuthnChallengeUsedAsync(challenge.Id);

        var originalOptions = CredentialCreateOptions.FromJson(challenge.OptionsJson);
        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(request.AttestationResponseJson);
        if (attestationResponse is null) return null;

        RegisteredPublicKeyCredential result;
        try
        {
            result = await rp.Fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions     = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (p, _) =>
                    await _data.GetWebAuthnCredentialByCredentialIdAsync(Base64Url.Encode(p.CredentialId)) is null
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn registration verification failed for user {UserId}", familyUserId);
            return null;
        }

        var credential = await _data.CreateWebAuthnCredentialAsync(new WebAuthnCredential
        {
            FamilyUserId    = familyUserId,
            RegisteredAppId = rp.App.Id,
            RpId            = rp.RpId,
            CredentialId    = Base64Url.Encode(result.Id),
            PublicKey       = Convert.ToBase64String(result.PublicKey),
            SignCount       = result.SignCount,
            UserHandle      = Base64Url.Encode(result.User.Id),
            AaGuid          = result.AaGuid == Guid.Empty ? null : result.AaGuid,
            Transports      = JsonSerializer.Serialize((result.Transports ?? []).Select(t => t.ToString())),
            DeviceLabel     = request.DeviceLabel
        });

        await _data.WriteAuditLogAsync(new AuditLog
        {
            FamilyUserId = familyUserId,
            Action       = "WebAuthnCredentialRegistered",
            AppClientId  = rp.App.ClientId
        });

        return new RegisterCompleteResponse(credential.Id, credential.CredentialId, credential.CreatedAt);
    }

    public async Task<WebAuthnLoginOptionsResponse?> GetLoginOptionsAsync(WebAuthnLoginOptionsRequest request, string? origin)
    {
        var rp = await ResolveRpContextAsync(request.AppClientId, origin);
        if (rp is null) return null;

        var user  = await _data.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant());
        var creds = user is not null && user.IsActive
            ? await _data.GetWebAuthnCredentialsByUserAndRpIdAsync(user.Id, rp.RpId)
            : [];

        var allowedCredentials = creds
            .Select(c => new PublicKeyCredentialDescriptor(Base64Url.Decode(c.CredentialId)))
            .ToList();

        var options = rp.Fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification   = UserVerificationRequirement.Required
        });

        var optionsJson = JsonSerializer.Serialize(options);

        // Unknown email or zero credentials for this RP still gets a same-shaped response — no
        // real challenge is persisted, so login-complete simply fails to find a match. This avoids
        // confirming which emails have a passkey registered.
        if (user is null || !user.IsActive || creds.Count == 0)
            return new WebAuthnLoginOptionsResponse(Guid.NewGuid().ToString("N"), optionsJson);

        var challenge = await _data.CreateWebAuthnChallengeAsync(new WebAuthnChallenge
        {
            FamilyUserId    = user.Id,
            RegisteredAppId = rp.App.Id,
            RpId            = rp.RpId,
            ChallengeToken  = Guid.NewGuid().ToString("N"),
            ChallengeKind   = AssertionChallengeKind,
            OptionsJson     = optionsJson,
            ExpiresAt       = DateTime.UtcNow.Add(ChallengeLifetime)
        });

        return new WebAuthnLoginOptionsResponse(challenge.ChallengeToken, optionsJson);
    }

    public async Task<LoginResponse?> CompleteLoginAsync(WebAuthnLoginCompleteRequest request, string? origin, string? ipAddress)
    {
        var challenge = await _data.GetWebAuthnChallengeAsync(request.ChallengeToken);
        if (challenge is null || challenge.ChallengeKind != AssertionChallengeKind) return null;

        // Re-validate the Origin header against the challenge's own app/RP — a credential
        // registered for one origin must never be assertable from a different one, even under
        // the same AppClientId.
        if (!TryValidateOrigin(challenge.App, origin, out var rpId) || rpId != challenge.RpId) return null;

        await _data.MarkWebAuthnChallengeUsedAsync(challenge.Id);

        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(request.AssertionResponseJson);
        if (assertionResponse is null) return null;

        var storedCredential = await _data.GetWebAuthnCredentialByCredentialIdAsync(Base64Url.Encode(assertionResponse.RawId));
        if (storedCredential is null || storedCredential.RpId != challenge.RpId) return null;

        var config = new Fido2Configuration
        {
            ServerDomain = challenge.RpId,
            ServerName   = challenge.App.Name,
            Origins      = new HashSet<string> { origin! }
        };
        var fido2 = _fido2Factory.Create(config);
        var originalOptions = AssertionOptions.FromJson(challenge.OptionsJson);

        VerifyAssertionResult result;
        try
        {
            result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse      = assertionResponse,
                OriginalOptions        = originalOptions,
                StoredPublicKey        = Convert.FromBase64String(storedCredential.PublicKey),
                StoredSignatureCounter = (uint)storedCredential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = async (p, _) =>
                    Base64Url.Encode(p.UserHandle) == storedCredential.UserHandle
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAuthn assertion verification failed for credential {CredentialId}", storedCredential.CredentialId);
            return null;
        }

        await _data.UpdateWebAuthnCredentialSignCountAsync(storedCredential.Id, result.SignCount);

        var user = storedCredential.User;
        if (!user.IsActive || user.IsWard) return null;

        var app    = challenge.App;
        var access = await _data.GetAppAccessAsync(user.Id, app.Id);
        if (access is null) return null;

        var permissions = await _data.GetPermissionsForUserAsync(user.Id);
        var token        = _jwt.GenerateToken(user, permissions, app.ClientId, access.AppRole);

        string? refresh = null;
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
        await _data.WriteAuditLogAsync(new AuditLog
        {
            FamilyUserId = user.Id,
            Action       = "WebAuthnLogin",
            IpAddress    = ipAddress,
            AppClientId  = app.ClientId
        });

        return new LoginResponse(token, refresh, DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            user.Id, user.FullName, user.Email, user.Role, user.MustChangePassword);
    }
}
