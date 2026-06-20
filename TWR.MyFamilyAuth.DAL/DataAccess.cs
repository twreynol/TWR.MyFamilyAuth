using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using TWR.MyFamilyAuth.DAL.Interfaces;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess : IDataAccess
{
    private readonly string _connectionString;
    private readonly ILogger<DataAccess> _logger;

    public DataAccess(IConfiguration configuration, ILogger<DataAccess> logger)
    {
        _logger = logger;

        var rawCs = configuration.GetConnectionString("PostgreSQL");

        if (string.IsNullOrEmpty(rawCs))
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                var uri      = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':');
                rawCs = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Disable";
            }
        }

        if (string.IsNullOrEmpty(rawCs))
            throw new InvalidOperationException("No PostgreSQL connection string found.");

        var builder = new NpgsqlConnectionStringBuilder(rawCs);
        if (builder.KeepAlive == 0) builder.KeepAlive = 30;
        _connectionString = builder.ConnectionString;
    }

    public void ApplyMigrations()
    {
        try
        {
            using var db = CreateContext();
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count > 0)
            {
                _logger.LogInformation("Applying {Count} pending migration(s): {Names}",
                    pending.Count, string.Join(", ", pending));
                db.Database.Migrate();
            }
            else
            {
                _logger.LogInformation("Database schema is up-to-date.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }

    private MyFamilyAuthDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MyFamilyAuthDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}
