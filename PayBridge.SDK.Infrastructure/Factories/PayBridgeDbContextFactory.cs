using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PayBridge.SDK.Infrastructure.Externsions;
using PayBridge.SDK.Infrastructure.Persistence;

namespace PayBridge.SDK.Infrastructure.Factories;

public class PayBridgeDbContextFactory : IDesignTimeDbContextFactory<PayBridgeDbContext>
{
    public PayBridgeDbContext CreateDbContext(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddBasePath()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PayBridgeDbContext>();
        var connectionString = config.GetConnectionString(nameof(PayBridgeDbContext));
        
        var providerConfigured = config["DatabaseProvider"] ?? "MSSQL";
        if (providerConfigured == "MSSQL")
            optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        else if (providerConfigured == "PostgreSQL")
            optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        else if (providerConfigured == "MySQL")
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        else if (providerConfigured == "SQLite")
            optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        else
            optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        return new PayBridgeDbContext(optionsBuilder.Options);
    }
}
