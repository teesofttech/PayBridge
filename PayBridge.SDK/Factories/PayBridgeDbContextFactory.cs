﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PayBridge.SDK;

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
            optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("PayBridge.SDK"));
        else if (providerConfigured == "PostgreSQL")
            optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("PayBridge.SDK"));
        else if (providerConfigured == "MySQL")
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b => b.MigrationsAssembly("PayBridge.SDK"));
        else if (providerConfigured == "SQLite")
            optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("PayBridge.SDK"));
        else
            optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("PayBridge.SDK"));
        return new PayBridgeDbContext(optionsBuilder.Options);
    }
}
