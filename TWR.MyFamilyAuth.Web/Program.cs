using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Serilog;
using TWR.MyFamilyAuth.Web.Services;
using TWR.Shared.Auth.Services;
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

// MyFamilyAuth.Web IS MyFamilyAuth — no proxy involved, so both the "app API" and the
// "MyFamilyAuth direct" clients AddTwrSharedAuth wires up point at the same base URL.
builder.Services.AddTwrSharedAuth(appClientId: "myfamilyauth", appApiBaseUrl: apiBaseUrl, myFamilyAuthPublicBaseUrl: apiBaseUrl);

builder.Services.AddScoped<BuildInfoService>();
builder.Services.AddScoped<IBuddyManagementService, BuddyManagementService>();

var host = builder.Build();

// Wires AuthTokenStore's refresh/logout callbacks to the built AuthService instance, then
// restores a session from a stored refresh token before the app renders.
await host.Services.InitializeTwrSharedAuthAsync();

await host.RunAsync();
