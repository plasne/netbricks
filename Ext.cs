using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NetBricks;

public static class Ext
{
    public static IServiceCollection AddConfig<T>(this IServiceCollection services) where T : class, IConfig
    {
        services.TryAddSingleton<IConfig, T>();
        return services;
    }

    public static IServiceCollection AddConfig(this IServiceCollection services)
    {
        services.TryAddSingleton<IConfig, Config>();
        return services;
    }

    public static IServiceCollection AddDefaultAzureCredential(this IServiceCollection services)
    {
        services.TryAddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IConfig>();
            return config.DefaultAzureCredential;
        });
        return services;
    }

    public static IServiceCollection AddDefaultAzureCredentials(this IServiceCollection services, IConfig config)
    {
        services.TryAddSingleton(config.DefaultAzureCredential);
        return services;
    }
}