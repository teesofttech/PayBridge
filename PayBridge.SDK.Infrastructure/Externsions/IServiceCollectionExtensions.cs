using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayBridge.SDK.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Infrastructure.Externsions;
public static class IServiceCollectionExtensions
{
    private const int RetryCount = 3;
    private const double RetryInSeconds = 1.1;
    //public static IServiceCollection AddPersistenceLayer(this IServiceCollection services, IConfiguration configuration)
    //{
    //    services.AddRepositories();
    //    return services;
    //}

    public static IServiceCollection AddDBRepository(this IServiceCollection services, IConfiguration configuration, string provider)
    {
        var connectionString = configuration.GetConnectionString(nameof(PayBridgeDbContext));
        if (provider == "MSSQL")
        {
            services.AddDbContext<PayBridgeDbContext>(options =>
            {
                options.UseSqlServer(connectionString,
                opt =>
                opt.EnableRetryOnFailure(
                   RetryCount,
                    TimeSpan.FromSeconds(
                        RetryInSeconds),
                    null));
            }, ServiceLifetime.Scoped);
        }
        else if (provider == "PostgreSQL")
        {
            services.AddDbContext<PayBridgeDbContext>(options =>
            {
                options.UseNpgsql(connectionString,
                opt =>
                opt.EnableRetryOnFailure(
                   RetryCount,
                    TimeSpan.FromSeconds(
                        RetryInSeconds),
                    null));
            }, ServiceLifetime.Scoped);
        }
        else if (provider == "MySQL")
        {
            //services.AddDbContext<PayBridgeDbContext>(options =>
            //{
            //    options.UseMySql(connectionString,
            //    opt =>
            //    opt.EnableRetryOnFailure(
            //       RetryCount,
            //        TimeSpan.FromSeconds(
            //            RetryInSeconds),
            //        null));
            //}, ServiceLifetime.Scoped);
        }
        else
        {
            throw new NotSupportedException($"The provider '{provider}' is not supported.");
        }

        return services;
    }
}
