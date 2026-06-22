using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Extensions.Logging;
using System.Text;
using TWR.MyFamilyAuth.API.AppServices;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.DAL;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (!builder.Environment.IsDevelopment())
            builder.WebHost.UseUrls("http://0.0.0.0:8080");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting TWR.MyFamilyAuth.API");

            RegisterServices(builder.Services, builder.Configuration);
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new SerilogLoggerProvider(Log.Logger));

            var app = builder.Build();

            // Migrations
            var data = app.Services.GetRequiredService<IDataAccess>();
            data.ApplyMigrations();

            // Seed SuperAdmin
            var adminEmail    = app.Configuration["Seed:SuperAdminEmail"];
            var adminFirst    = app.Configuration["Seed:SuperAdminFirstName"] ?? "Tim";
            var adminLast     = app.Configuration["Seed:SuperAdminLastName"]  ?? "Reynolds";
            var adminPassword = app.Configuration["Seed:SuperAdminPassword"];
            if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                await data.SeedSuperAdminAsync(adminEmail, adminFirst, adminLast, hash);
                Log.Information("SuperAdmin seeded: {Email}", adminEmail);
            }

            // Register MyFamilyAuth itself as an app
            var selfApp = await data.GetRegisteredAppByClientIdAsync("myfamilyauth");
            if (selfApp is null)
            {
                selfApp = await data.CreateRegisteredAppAsync(new TWR.MyFamilyAuth.DAL.Entities.RegisteredApp
                {
                    Name             = "MyFamilyAuth",
                    ClientId         = "myfamilyauth",
                    ClientSecretHash = string.Empty,
                    AllowedOrigins   = "[\"https://localhost:7288\",\"http://localhost:5288\"]",
                    SupportedRoles   = "[\"SuperAdmin\",\"FamilyAdmin\",\"User\"]",
                    IsActive         = true
                });
                Log.Information("Registered MyFamilyAuth app (ClientId: myfamilyauth)");
            }

            // Grant SuperAdmin access to MyFamilyAuth app
            TWR.MyFamilyAuth.DAL.Entities.FamilyUser? superAdmin = null;
            if (!string.IsNullOrEmpty(adminEmail))
            {
                superAdmin = await data.GetUserByEmailAsync(adminEmail);
                if (superAdmin is not null)
                {
                    await data.GrantAppAccessAsync(new TWR.MyFamilyAuth.DAL.Entities.AppAccess
                    {
                        FamilyUserId    = superAdmin.Id,
                        RegisteredAppId = selfApp.Id,
                        AppRole         = "SuperAdmin",
                        GrantedByUserId = superAdmin.Id
                    });
                    Log.Information("Granted SuperAdmin access to MyFamilyAuth for {Email}", adminEmail);
                }
            }

            // Register MyFinances as an app with 2FA required
            var finApp = await data.GetRegisteredAppByClientIdAsync("myfinances");
            if (finApp is null)
            {
                finApp = await data.CreateRegisteredAppAsync(new TWR.MyFamilyAuth.DAL.Entities.RegisteredApp
                {
                    Name             = "MyFinances",
                    ClientId         = "myfinances",
                    ClientSecretHash = string.Empty,
                    AllowedOrigins   = "[\"https://localhost:7237\",\"http://localhost:5197\"]",
                    SupportedRoles   = "[\"Owner\",\"Viewer\"]",
                    IsActive         = true,
                    Requires2FA      = true
                });
                Log.Information("Registered MyFinances app (ClientId: myfinances, 2FA: required)");
            }

            // Grant SuperAdmin access to MyFinances
            if (superAdmin is not null)
            {
                var existingAccess = await data.GetAppAccessAsync(superAdmin.Id, finApp.Id);
                if (existingAccess is null)
                {
                    await data.GrantAppAccessAsync(new TWR.MyFamilyAuth.DAL.Entities.AppAccess
                    {
                        FamilyUserId    = superAdmin.Id,
                        RegisteredAppId = finApp.Id,
                        AppRole         = "Owner",
                        GrantedByUserId = superAdmin.Id
                    });
                    Log.Information("Granted Owner access to MyFinances for {Email}", adminEmail);
                }
            }

            app.UseCors("WebClients");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHealthChecks("/health");

            await app.RunAsync();
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Fatal(ex, "Application terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(nameof(JwtSettings)));
        services.Configure<EmailSettings>(configuration.GetSection(nameof(EmailSettings)));

        services.AddSingleton<IDataAccess, DataAccess>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IEmailService, EmailService>();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy("WebClients", p =>
            p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

        var jwtSettings = configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>();
        if (jwtSettings is not null)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidAudience            = jwtSettings.Audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
                });
        }

        services.AddAuthorization();
        services.AddHealthChecks();
        services.AddControllers();
    }
}
