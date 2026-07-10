using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Serilog;
using TWR.MyFamilyAuth.Web.Services;
using TWR.Shared.Components.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<TWR.MyFamilyAuth.Web.App>("#app");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.BrowserConsole()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

var apiBaseUrl = string.IsNullOrEmpty(builder.Configuration["ApiSettings:BaseUrl"])
    ? builder.HostEnvironment.BaseAddress
    : builder.Configuration["ApiSettings:BaseUrl"]!;

// AuthTokenStore is a singleton shared by AuthService and RefreshTokenHandler.
// It holds the current tokens and the refresh/logout callbacks wired after Build().
builder.Services.AddSingleton<AuthTokenStore>();

// HttpClient — RefreshTokenHandler sits in front of every non-auth API call, silently
// refreshing an expired access token via the refresh token instead of logging the user out.
builder.Services.AddScoped(sp =>
{
    var store   = sp.GetRequiredService<AuthTokenStore>();
    var handler = new RefreshTokenHandler(store) { InnerHandler = new HttpClientHandler() };
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthService>());
builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
builder.Services.AddScoped<BuildInfoService>();
builder.Services.AddScoped<IBuddyManagementService, BuddyManagementService>();

var host = builder.Build();

// Wire refresh/logout callbacks into the token store.
// In Blazor WASM, scoped services have application lifetime, so this resolves
// the same AuthService instance that the rest of the app will use.
var tokenStore  = host.Services.GetRequiredService<AuthTokenStore>();
var authService = host.Services.GetRequiredService<AuthService>();
tokenStore.TryRefreshAsync = () => authService.TryRefreshAsync();
tokenStore.LogoutAsync     = () => authService.LogoutAsync();

// Restore a session from a stored refresh token (e.g. after a page reload) before
// the app renders, so an authenticated user doesn't briefly flash as logged-out.
await authService.InitializeAsync();

await host.RunAsync();
