using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NAITSSMS.Infrastructure.Data;

/// <summary>
/// Allows EF Core design-time tools (migrations, scaffolding) to create
/// an AppDbContext without needing the full ASP.NET host to start.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Walk up from Infrastructure bin to find the API appsettings
        var basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "NAITSSMS.API");

        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Ensure appsettings.Development.json exists in NAITSSMS.API.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly("NAITSSMS.Infrastructure"));

        return new AppDbContext(optionsBuilder.Options);
    }
}
