using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PayBridge.SDK.Infrastructure.Externsions;
using PayBridge.SDK.Infrastructure.Persistence;

namespace PayBridge.SDK.Infrastructure.Factories;

public class HopRideContextFactory : IDesignTimeDbContextFactory<PayBridgeDbContext>
{
    public PayBridgeDbContext CreateDbContext(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddBasePath()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PayBridgeDbContext>();
        var connectionString = config.GetConnectionString(nameof(PayBridgeDbContext));
        optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("PayBridge.SDK.Infrastructure"));
        return new PayBridgeDbContext(optionsBuilder.Options);
    }
}
