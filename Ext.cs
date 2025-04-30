using System;
using System.Linq;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace NetBricks;

public static class Ext
{
    /// <summary>
    /// Add any configuration provider to the service collection.
    /// </summary>
    /// <typeparam name="T">The type of the configuration provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddConfig<T>(this IServiceCollection services) where T : class, IConfig
    {
        services.TryAddSingleton<IConfig, T>();
        return services;
    }

    /// <summary>
    /// Add the Netbricks configuration provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddConfig(this IServiceCollection services)
    {
        services.TryAddSingleton<IConfig, Config>();
        return services;
    }

    /// <summary>
    /// Add the Netbricks configuration provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHttpClientForConfig(this IServiceCollection services)
    {
        services.AddHttpClient("netbricks");
        return services;
    }

    /// <summary>
    /// Add the default Azure credential to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDefaultAzureCredential(this IServiceCollection services)
    {
        // get the list of credential options
        string[] include = (Config.INCLUDE_CREDENTIAL_TYPES.Length > 0)
            ? Config.INCLUDE_CREDENTIAL_TYPES :
            string.Equals(Config.ASPNETCORE_ENVIRONMENT, "Development", StringComparison.InvariantCultureIgnoreCase)
                ? ["azcli", "env"]
                : ["env", "mi"];

        // log
        Console.WriteLine($"INCLUDE_CREDENTIAL_TYPES = \"{string.Join(", ", include)}\"");

        // add as a singleton
        services.TryAddSingleton(service => new DefaultAzureCredential(
            new DefaultAzureCredentialOptions()
            {
                ExcludeEnvironmentCredential = !include.Contains("env"),
                ExcludeManagedIdentityCredential = !include.Contains("mi"),
                ExcludeSharedTokenCacheCredential = !include.Contains("token"),
                ExcludeVisualStudioCredential = !include.Contains("vs"),
                ExcludeVisualStudioCodeCredential = !include.Contains("vscode"),
                ExcludeAzureCliCredential = !include.Contains("azcli"),
                ExcludeInteractiveBrowserCredential = !include.Contains("browser"),
                ExcludeAzureDeveloperCliCredential = !include.Contains("azd"),
                ExcludeAzurePowerShellCredential = !include.Contains("ps"),
                ExcludeWorkloadIdentityCredential = !include.Contains("workload"),
            }));

        return services;
    }

    /// <summary>
    /// Add the single line console logger to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logParams">Whether to log the logging parameters.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSingleLineConsoleLogger(this IServiceCollection services, bool logParams = true)
    {
        // log the logger variables
        if (logParams)
        {
            Console.WriteLine($"LOG_LEVEL = \"{Config.LOG_LEVEL}\"");
            Console.WriteLine($"DISABLE_COLORS = \"{Config.DISABLE_COLORS}\"");
        }

        // add the logger
        services
            .AddLogging(configure =>
            {
                services.TryAddSingleton<ILoggerProvider, SingleLineConsoleLoggerProvider>();
            })
            .Configure<LoggerFilterOptions>(options =>
            {
                options.MinLevel = Config.LOG_LEVEL;
            });

        return services;
    }
}