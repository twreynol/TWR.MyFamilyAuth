using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.Helpers;

namespace TWR.MyFamilyAuth.Web.Services;

public class AuthService : AuthenticationStateProvider, IAuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private string? _token;
    private string? _refreshToken;
    private string? _pendingAppClientId;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js   = js;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_currentUser));

    public string? LastLoginError         { get; private set; }
    public string? PendingChallengeToken  { get; private set; }
    public bool    MustChangePassword     { get; private set; }

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

    private Task CompleteLoginAsync(LoginResponse result)
    {
        _token             = result.Token;
        _refreshToken      = result.RefreshToken;
        MustChangePassword = result.MustChangePassword;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new Claim(ClaimTypes.Name,           result.FullName),
            new Claim(ClaimTypes.Email,          result.Email),
            new Claim(ClaimTypes.Role,           result.Role)
        }, "jwt");

        _currentUser = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try { await _http.PostAsJsonAsync($"{ApiRoutes.Auth}/logout", new RefreshTokenRequest(_refreshToken)); }
            catch { /* best effort */ }
        }
        _token        = null;
        _refreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
        _currentUser  = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public string? GetToken() => _token;

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
