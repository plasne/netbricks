using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetBricks;

public static class Ext
{
    private static readonly object addLock = new();
    private static readonly HashSet<Type> added = [];

    // NOTE: these do not use the Options pattern because all consumers are not guaranteed to get the same Options object (ex. race
    // conditions, different scopes, etc.)

    /// <summary>
    /// Adds the Azure App Configuration service to the service collection.
    /// This method configures Azure App Configuration integration so that settings can be loaded from Azure App Configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logMethod">The method to use for logging.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzureAppConfig(this IServiceCollection services, LogMethod logMethod = LogMethod.ILogger)
    {
        // check if already added
        lock (addLock)
        {
            if (added.Contains(typeof(AzureAppConfig)))
                return services;

            // add the config options
            services.AddSingleton<AzureAppConfigOptions>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var options = new AzureAppConfigOptions();

                // set APPCONFIG_URL
                var APPCONFIG_URL = configuration.GetValue<string>("APPCONFIG")
                    ?? configuration.GetValue<string>("APPCONFIG_URL");
                if (!string.IsNullOrEmpty(APPCONFIG_URL))
                {
                    APPCONFIG_URL = APPCONFIG_URL.ToLower();
                    if (!APPCONFIG_URL.Contains(".azconfig.io")) APPCONFIG_URL += ".azconfig.io";
                    if (!APPCONFIG_URL.StartsWith("https://")) APPCONFIG_URL = "https://" + APPCONFIG_URL;
                }
                options.APPCONFIG_URL = APPCONFIG_URL;

                // set APPCONFIG_KEYS
                options.APPCONFIG_KEYS = configuration.GetValue<string>("APPCONFIG_KEYS").AsArray() ?? [];

                // set APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS
                options.APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS = configuration.GetValue<string>("APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS").AsBool() ?? false;

                // log the parameters
                if (logMethod == LogMethod.ILogger)
                {
                    var logger = provider.GetRequiredService<ILogger<AzureAppConfigOptions>>();
                    logger.LogInformation($"APPCONFIG_URL = \"{options.APPCONFIG_URL}\"");
                    logger.LogInformation($"APPCONFIG_KEYS = \"{string.Join(", ", options.APPCONFIG_KEYS)}\"");
                    logger.LogInformation($"APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS = \"{options.APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS}\"");
                }
                else
                {
                    Console.WriteLine("AzureAppConfigOptions:");
                    Console.WriteLine($"  APPCONFIG_URL = \"{options.APPCONFIG_URL}\"");
                    Console.WriteLine($"  APPCONFIG_KEYS = \"{string.Join(", ", options.APPCONFIG_KEYS)}\"");
                    Console.WriteLine($"  APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS = \"{options.APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS}\"");
                }

                return options;
            });

            // add AzureAppConfig as a service
            services.AddSingleton<AzureAppConfig>();

            // add the startup services
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OptionsStartup>());
            services.AddHostedService<AzureAppConfigStartup>();

            added.Add(typeof(AzureAppConfig));
        }

        return services;
    }

    /// <summary>
    /// Adds a strongly-typed configuration object to the service collection.
    /// This method registers the config class as both the interface and implementation.
    /// </summary>
    /// <typeparam name="T">The type of configuration class to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="logMethod">The method to use for logging.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfig<T>(this IServiceCollection services, LogMethod logMethod = LogMethod.ILogger)
        where T : class
    {
        return AddConfig<T, T>(services, logMethod);
    }

    /// <summary>
    /// Adds a strongly-typed configuration object to the service collection with support for interface/implementation separation.
    /// This method configures Azure App Configuration integration, validation, and value resolution from various sources.
    /// The configuration object supports:
    /// - Loading values from Azure App Configuration
    /// - Custom value setting via [SetValue] and [SetValues] attributes
    /// - Resolving secrets from Key Vault references
    /// - Data validation via validation attributes
    /// - Console output of configuration values via [WriteToConsole] attributes
    /// </summary>
    /// <typeparam name="I">The interface type to register.</typeparam>
    /// <typeparam name="T">The implementation type to instantiate and configure.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="logMethod">The method to use for logging.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfig<I, T>(this IServiceCollection services, LogMethod logMethod = LogMethod.ILogger)
        where I : class
        where T : class, I
    {
        // check if already added
        lock (addLock)
        {
            if (added.Contains(typeof(I)))
                return services;

            // add the config object
            services.AddSingleton<ConfigOptions>(provider => new ConfigOptions { LogMethod = logMethod });

            // add the factory
            services.AddSingleton<IConfigFactory<I>, ConfigFactory<I, T>>();

            // add the startup services
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OptionsStartup>());
            services.AddHostedService<ConfigStartup<I>>(provider =>
            {
                var configFactory = provider.GetRequiredService<IConfigFactory<I>>();
                return new ConfigStartup<I>(configFactory, logMethod);
            });

            added.Add(typeof(I));
        }

        return services;
    }

    /// <summary>
    /// Add the default Azure credential to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logMethod">The method to use for logging.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDefaultAzureCredential(this IServiceCollection services, LogMethod logMethod = LogMethod.ILogger)
    {
        // check if already added
        lock (addLock)
        {
            if (added.Contains(typeof(DefaultAzureCredential)))
                return services;

            services.AddSingleton<DefaultAzureCredentialOptions>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var options = new DefaultAzureCredentialOptions();

                // get the variables
                var INCLUDE_CREDENTIAL_TYPES = configuration.GetValue<string>("INCLUDE_CREDENTIAL_TYPES").AsArray();
                var ASPNETCORE_ENVIRONMENT = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT");
                var AZURE_CLIENT_ID = configuration.GetValue<string>("AZURE_CLIENT_ID");
                string[] include = (INCLUDE_CREDENTIAL_TYPES is not null && INCLUDE_CREDENTIAL_TYPES.Length > 0)
                    ? INCLUDE_CREDENTIAL_TYPES
                    : string.Equals(ASPNETCORE_ENVIRONMENT, "Development", StringComparison.OrdinalIgnoreCase)
                        ? ["azcli", "env"]
                        : ["env", "mi"];

                // log
                if (logMethod == LogMethod.ILogger)
                {
                    var logger = provider.GetRequiredService<ILogger<DefaultAzureCredentialOptions>>();
                    logger.LogInformation($"ASPNETCORE_ENVIRONMENT = \"{ASPNETCORE_ENVIRONMENT}\"");
                    logger.LogInformation($"INCLUDE_CREDENTIAL_TYPES = \"{string.Join(", ", include)}\"");
                    logger.LogInformation($"AZURE_CLIENT_ID = \"{AZURE_CLIENT_ID}\"");
                }
                else
                {
                    Console.WriteLine("DefaultAzureCredentialOptions:");
                    Console.WriteLine($"  ASPNETCORE_ENVIRONMENT = \"{ASPNETCORE_ENVIRONMENT}\"");
                    Console.WriteLine($"  INCLUDE_CREDENTIAL_TYPES = \"{string.Join(", ", include)}\"");
                    Console.WriteLine($"  AZURE_CLIENT_ID = \"{AZURE_CLIENT_ID}\"");
                }

                // set the options
                options.ManagedIdentityClientId = AZURE_CLIENT_ID;
                options.ExcludeEnvironmentCredential = !include.Contains("env");
                options.ExcludeManagedIdentityCredential = !include.Contains("mi");
                options.ExcludeSharedTokenCacheCredential = !include.Contains("token");
                options.ExcludeVisualStudioCredential = !include.Contains("vs");
                options.ExcludeVisualStudioCodeCredential = !include.Contains("vscode");
                options.ExcludeAzureCliCredential = !include.Contains("azcli");
                options.ExcludeInteractiveBrowserCredential = !include.Contains("browser");
                options.ExcludeAzureDeveloperCliCredential = !include.Contains("azd");
                options.ExcludeAzurePowerShellCredential = !include.Contains("ps");
                options.ExcludeWorkloadIdentityCredential = !include.Contains("workload");

                return options;
            });

            // add as a singleton
            services.AddSingleton<DefaultAzureCredential>(provider =>
            {
                var options = provider.GetRequiredService<DefaultAzureCredentialOptions>();
                return new DefaultAzureCredential(options);
            });

            // add the startup services
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OptionsStartup>());

            added.Add(typeof(DefaultAzureCredential));
        }

        return services;
    }

    /// <summary>
    /// Add the single line console logger to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="logMethod">The method to use for logging.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSingleLineConsoleLogger(this IServiceCollection services, LogMethod logMethod = LogMethod.ILogger)
    {
        // check if already added
        lock (addLock)
        {
            if (added.Contains(typeof(SingleLineConsoleLogger)))
                return services;

            // add the options
            services.AddSingleton<SingleLineConsoleLoggerOptions>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var options = new SingleLineConsoleLoggerOptions();

                // set the options
                var logLevel = configuration.GetValue<string>("LOG_LEVEL").AsEnum<LogLevel>() ?? LogLevel.Information;
                options.LOG_WITH_COLORS = configuration.GetValue<string>("LOG_WITH_COLORS").AsBool() ?? true;

                // log the parameters
                if (logMethod == LogMethod.ILogger)
                {
                    options.LOG_TO_CONSOLE = true;
                }
                else
                {
                    Console.WriteLine("SingleLineConsoleLoggerOptions:");
                    Console.WriteLine($"  LOG_LEVEL = \"{logLevel}\"");
                    Console.WriteLine($"  LOG_WITH_COLORS = \"{options.LOG_WITH_COLORS}\"");
                }

                return options;
            });

            // add the logger filter options
            services.AddOptions<LoggerFilterOptions>().Configure<IConfiguration>((options, configuration) =>
            {
                options.MinLevel = configuration.GetValue<string>("LOG_LEVEL").AsEnum<LogLevel>() ?? LogLevel.Information;
            });

            // add the logger
            services
                .AddLogging(configure =>
                {
                    services.TryAddSingleton<ILoggerProvider, SingleLineConsoleLoggerProvider>();
                });

            // add the startup services
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OptionsStartup>());

            added.Add(typeof(SingleLineConsoleLogger));
        }

        return services;
    }
}