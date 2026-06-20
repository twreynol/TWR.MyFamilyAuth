using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TWR.MyFamilyAuth.DAL;

public class MyFamilyAuthDbContextFactory : IDesignTimeDbContextFactory<MyFamilyAuthDbContext>
{
    public MyFamilyAuthDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "TWR.MyFamilyAuth.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("No PostgreSQL connection string found.");

        var options = new DbContextOptionsBuilder<MyFamilyAuthDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MyFamilyAuthDbContext(options);
    }
}
