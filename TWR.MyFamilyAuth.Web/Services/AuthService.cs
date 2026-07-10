using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.Helpers;

namespace TWR.MyFamilyAuth.Web.Services;

public class AuthService : AuthenticationStateProvider, IAuthService
{
    private const string RefreshTokenStorageKey = "mfa_refresh_token";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly AuthTokenStore _store;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private string? _pendingAppClientId;

    public AuthService(HttpClient http, IJSRuntime js, AuthTokenStore store)
    {
        _http  = http;
        _js    = js;
        _store = store;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_currentUser));

    public string? LastLoginError         { get; private set; }
    public string? PendingChallengeToken  { get; private set; }
    public bool    MustChangePassword     { get; private set; }

    /// <summary>
    /// Called once at startup after the host is built. Restores a session from a
    /// refresh token left in localStorage (e.g. after a page reload), by exchanging
    /// it for a fresh access token — never trusts a locally-cached access token itself.
    /// </summary>
    public async Task InitializeAsync()
    {
        string? storedRefreshToken;
        try
        {
            storedRefreshToken = await _js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenStorageKey);
        }
        catch { return; } // JS interop not ready (prerender) — nothing to restore yet.

        if (string.IsNullOrEmpty(storedRefreshToken))
            return;

        _store.RefreshToken = storedRefreshToken;
        if (!await TryRefreshAsync())
            await ClearStoredRefreshTokenAsync();
    }

    public async Task<bool> LoginAsync(LoginRequest request)
    {
        LastLoginError        = null;
        PendingChallengeToken = null;
        try
        {
            var response = await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/login", request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                LastLoginError = $"HTTP {(int)response.StatusCode}: {body}";
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            if (result.RequiresTwoFactor)
            {
                PendingChallengeToken = result.TwoFactorChallengeToken;
                _pendingAppClientId   = request.AppClientId;
                return false;
            }

            await CompleteLoginAsync(result);
            return true;
        }
        catch (Exception ex) { LastLoginError = ex.Message; return false; }
    }

    public async Task<bool> VerifyTwoFactorAsync(string otpCode, bool trustDevice)
    {
        LastLoginError = null;
        if (PendingChallengeToken is null) return false;
        try
        {
            var verifyReq = new VerifyTwoFactorRequest(PendingChallengeToken, otpCode, trustDevice);
            var response  = await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/verify-2fa", verifyReq);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                LastLoginError = $"HTTP {(int)response.StatusCode}: {body}";
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            if (!string.IsNullOrEmpty(result.DeviceTrustToken) && !string.IsNullOrEmpty(_pendingAppClientId))
            {
                await _js.InvokeVoidAsync("localStorage.setItem",
                    $"device_trust_{_pendingAppClientId}", result.DeviceTrustToken);
            }

            PendingChallengeToken = null;
            _pendingAppClientId   = null;
            await CompleteLoginAsync(result);
            return true;
        }
        catch (Exception ex) { LastLoginError = ex.Message; return false; }
    }

    /// <summary>
    /// Silently exchanges the current refresh token for a new access/refresh pair.
    /// Called by RefreshTokenHandler on a 401, and by InitializeAsync on startup.
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_store.RefreshToken))
            return false;

        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{ApiRoutes.Auth}/refresh", new RefreshTokenRequest(_store.RefreshToken));
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            await CompleteLoginAsync(result);
            return true;
        }
        catch { return false; }
    }

    private async Task CompleteLoginAsync(LoginResponse result)
    {
        _store.AccessToken  = result.Token;
        _store.RefreshToken = result.RefreshToken;
        MustChangePassword  = result.MustChangePassword;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _store.AccessToken);

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            try { await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenStorageKey, result.RefreshToken); }
            catch { /* best effort — in-memory session still works this page load */ }
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new Claim(ClaimTypes.Name,           result.FullName),
            new Claim(ClaimTypes.Email,          result.Email),
            new Claim(ClaimTypes.Role,           result.Role)
        }, "jwt");

        _currentUser = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_store.RefreshToken))
        {
            try { await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/logout", new RefreshTokenRequest(_store.RefreshToken)); }
            catch { /* best effort */ }
        }
        _store.AccessToken  = null;
        _store.RefreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        await ClearStoredRefreshTokenAsync();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task ClearStoredRefreshTokenAsync()
    {
        try { await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenStorageKey); }
        catch { /* best effort */ }
    }

    public string? GetToken() => _store.AccessToken;

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        LastLoginError = null;
        try
        {
            var response = await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/forgot-password", new { email });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                LastLoginError = $"HTTP {(int)response.StatusCode}: {body}";
                return false;
            }
            return true;
        }
        catch (Exception ex) { LastLoginError = ex.Message; return false; }
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        LastLoginError = null;
        try
        {
            var response = await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/reset-password", new { token, newPassword });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                LastLoginError = $"HTTP {(int)response.StatusCode}: {body}";
                return false;
            }
            return true;
        }
        catch (Exception ex) { LastLoginError = ex.Message; return false; }
    }
}
