using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NetBricks;

public static class Ext
{
    /// <summary>
    /// Add any configuration provider to the service collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddConfig<T>(this IServiceCollection services) where T : class, IConfig
    {
        services.TryAddSingleton<IConfig, T>();
        return services;
    }

    /// <summary>
    /// Add the Netbricks configuration provider to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddConfig(this IServiceCollection services)
    {
        services.TryAddSingleton<IConfig, Config>();
        return services;
    }

    /// <summary>
    /// Add the default Azure credential to the service collection, from the configuration provider.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddDefaultAzureCredential(this IServiceCollection services)
    {
        services.TryAddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IConfig>();
            return config.DefaultAzureCredential;
        });
        return services;
    }

    /// <summary>
    /// Add the default Azure credential to the service collection, given a configuration object.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IServiceCollection AddDefaultAzureCredentials(this IServiceCollection services, IConfig config)
    {
        services.TryAddSingleton(config.DefaultAzureCredential);
        return services;
    }
}