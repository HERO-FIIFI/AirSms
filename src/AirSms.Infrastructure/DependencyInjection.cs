using AirSms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirSms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AirSmsDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'AirSmsDatabase' was not found.");

        services.AddDbContext<AirSmsDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
