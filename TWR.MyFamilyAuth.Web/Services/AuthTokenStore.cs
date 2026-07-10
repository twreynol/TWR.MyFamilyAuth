namespace TWR.MyFamilyAuth.Web.Services;

/// <summary>
/// Singleton shared between AuthService and RefreshTokenHandler.
/// AuthService writes tokens and wires the callbacks on login.
/// RefreshTokenHandler reads the token on every request and invokes
/// TryRefreshAsync when a 401 is received.
/// </summary>
public class AuthTokenStore
{
    public string? AccessToken  { get; set; }
    public string? RefreshToken { get; set; }

    /// <summary>Called by RefreshTokenHandler on 401. Returns true if a new token was obtained.</summary>
    public Func<Task<bool>>? TryRefreshAsync { get; set; }

    /// <summary>Called by RefreshTokenHandler when refresh fails — clears auth state.</summary>
    public Func<Task>? LogoutAsync { get; set; }
}
