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

            // Register MyMessages as an app (no 2FA required)
            var msgApp = await data.GetRegisteredAppByClientIdAsync("mymessages");
            if (msgApp is null)
            {
                msgApp = await data.CreateRegisteredAppAsync(new TWR.MyFamilyAuth.DAL.Entities.RegisteredApp
                {
                    Name             = "MyMessages",
                    ClientId         = "mymessages",
                    ClientSecretHash = string.Empty,
                    AllowedOrigins   = "[\"http://localhost:5201\"]",
                    SupportedRoles   = "[\"Owner\",\"User\"]",
                    IsActive         = true,
                    Requires2FA      = false
                });
                Log.Information("Registered MyMessages app (ClientId: mymessages)");
            }

            // Grant SuperAdmin access to MyMessages
            if (superAdmin is not null)
            {
                var existingMsgAccess = await data.GetAppAccessAsync(superAdmin.Id, msgApp.Id);
                if (existingMsgAccess is null)
                {
                    await data.GrantAppAccessAsync(new TWR.MyFamilyAuth.DAL.Entities.AppAccess
                    {
                        FamilyUserId    = superAdmin.Id,
                        RegisteredAppId = msgApp.Id,
                        AppRole         = "Owner",
                        GrantedByUserId = superAdmin.Id
                    });
                    Log.Information("Granted Owner access to MyMessages for {Email}", adminEmail);
                }
            }

            // Register TheFamilyInfo as an app (no 2FA required)
            var fviApp = await data.GetRegisteredAppByClientIdAsync("thefamilyinfo");
            if (fviApp is null)
            {
                fviApp = await data.CreateRegisteredAppAsync(new TWR.MyFamilyAuth.DAL.Entities.RegisteredApp
                {
                    Name             = "TheFamilyInfo",
                    ClientId         = "thefamilyinfo",
                    ClientSecretHash = string.Empty,
                    AllowedOrigins   = "[\"http://localhost:5401\"]",
                    SupportedRoles   = "[\"Owner\",\"User\"]",
                    IsActive         = true,
                    Requires2FA      = false
                });
                Log.Information("Registered TheFamilyInfo app (ClientId: thefamilyinfo)");
            }

            // Grant SuperAdmin access to TheFamilyInfo
            if (superAdmin is not null)
            {
                var fviAccess = await data.GetAppAccessAsync(superAdmin.Id, fviApp.Id);
                if (fviAccess is null)
                {
                    await data.GrantAppAccessAsync(new TWR.MyFamilyAuth.DAL.Entities.AppAccess
                    {
                        FamilyUserId    = superAdmin.Id,
                        RegisteredAppId = fviApp.Id,
                        AppRole         = "Owner",
                        GrantedByUserId = superAdmin.Id
                    });
                    Log.Information("Granted Owner access to TheFamilyInfo for {Email}", adminEmail);
                }
            }

            // Register MyMedical as an app (2FA disabled until MyMedical implements the 2FA relay flow)
            var medApp = await data.GetRegisteredAppByClientIdAsync("mymedical");
            if (medApp is null)
            {
                medApp = await data.CreateRegisteredAppAsync(new TWR.MyFamilyAuth.DAL.Entities.RegisteredApp
                {
                    Name             = "MyMedical",
                    ClientId         = "mymedical",
                    ClientSecretHash = string.Empty,
                    AllowedOrigins   = "[\"http://localhost:5000\",\"http://localhost:5001\"]",
                    SupportedRoles   = "[\"Owner\",\"User\"]",
                    IsActive         = true,
                    Requires2FA      = false
                });
                Log.Information("Registered MyMedical app (ClientId: mymedical, 2FA: disabled)");
            }
            else if (medApp.Requires2FA)
            {
                // Patch existing record — 2FA relay not yet implemented in MyMedical V2
                await data.UpdateRegisteredAppAsync(medApp.Id, requires2Fa: false);
                Log.Information("Patched MyMedical app — disabled 2FA until relay flow is implemented");
            }

            // Grant SuperAdmin access to MyMedical
            if (superAdmin is not null)
            {
                var medAccess = await data.GetAppAccessAsync(superAdmin.Id, medApp.Id);
                if (medAccess is null)
                {
                    await data.GrantAppAccessAsync(new TWR.MyFamilyAuth.DAL.Entities.AppAccess
                    {
                        FamilyUserId    = superAdmin.Id,
                        RegisteredAppId = medApp.Id,
                        AppRole         = "Owner",
                        GrantedByUserId = superAdmin.Id
                    });
                    Log.Information("Granted Owner access to MyMedical for {Email}", adminEmail);
                }
            }

            // ── Seed BuddyGrants ────────────────────────────────────────────────
            // Idempotent — checks before inserting. These are the canonical V2 grants
            // for the Reynolds/Reynolds-adjacent family circle.
            // Production: same seeder runs after GUID alignment (see ProdMigrationRules.md).
            async Task SeedGrantAsync(Guid grantorId, Guid granteeId, string[] permissions)
            {
                var existing = await data.GetGrantBetweenAsync(grantorId, granteeId);
                if (existing is null)
                {
                    await data.CreateBuddyGrantAsync(new TWR.MyFamilyAuth.DAL.Entities.BuddyGrant
                    {
                        GrantorId   = grantorId,
                        GranteeId   = granteeId,
                        Permissions = permissions,
                        IsActive    = true,
                        GrantedAt   = DateTime.UtcNow
                    });
                    Log.Information("Seeded BuddyGrant {Grantor} -> {Grantee} [{Perms}]",
                        grantorId, granteeId, string.Join(",", permissions));
                }
            }

            var tim      = await data.GetUserByEmailAsync("twreynol@hotmail.com");
            var diane    = await data.GetUserByEmailAsync("tanddreynolds@gmail.com");
            var sarah    = await data.GetUserByEmailAsync("reynolds.sarahmarie@gmail.com");
            var liz      = await data.GetUserByEmailAsync("Eclark710@gmail.com");

            if (tim is not null && diane is not null)
            {
                // Full bidirectional access between Tim and Diane
                await SeedGrantAsync(tim.Id,   diane.Id, ["Medical","Info","Messaging","Finances","Admin"]);
                await SeedGrantAsync(diane.Id, tim.Id,   ["Medical","Info","Messaging","Finances","Admin"]);
            }
            if (tim is not null && sarah is not null)
                await SeedGrantAsync(tim.Id, sarah.Id, ["Medical","Info","Messaging"]);

            if (tim is not null && liz is not null)
                await SeedGrantAsync(tim.Id, liz.Id, ["Medical","Info","Messaging"]);

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

        services.AddMemoryCache();
        services.AddSingleton<IDataAccess, DataAccess>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IWebAuthnAppService, WebAuthnAppService>();
        services.AddSingleton<IFido2Factory, Fido2Factory>();

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
