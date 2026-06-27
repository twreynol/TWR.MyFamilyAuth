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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthService>());
builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
builder.Services.AddScoped<BuildInfoService>();
builder.Services.AddScoped<IBuddyManagementService, BuddyManagementService>();

await builder.Build().RunAsync();
