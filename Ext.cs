using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetBricks;

// TODO: pull out Azure AppConfig so it is standalone

public static class Ext
{
    public static IServiceCollection AddConfig<T>(this IServiceCollection services)
        where T : class
    {
        return AddConfig<T, T>(services);
    }

    public static IServiceCollection AddConfig<I, T>(this IServiceCollection services)
        where I : class
        where T : class, I
    {
        // add the config options
        services.AddOptions<ConfigOptions>().Configure<IConfiguration, ILogger<AzureAppConfig>>((options, configuration, logger) =>
        {
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
            Console.WriteLine("ConfigOptions:");
            Console.WriteLine($"  APPCONFIG_URL = \"{options.APPCONFIG_URL}\"");
            Console.WriteLine($"  APPCONFIG_KEYS = \"{string.Join(", ", options.APPCONFIG_KEYS)}\"");
            Console.WriteLine($"  APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS = \"{options.APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS}\"");
        });

        // add the config object
        services.AddSingleton<I>(provider =>
        {
            // TODO: is there a better way to handle these async calls?

            // load Azure App Config
            var azureAppConfig = provider.GetRequiredService<AzureAppConfig>();
            azureAppConfig.LoadAsync().GetAwaiter().GetResult();

            // set the values on the config object
            var configuration = provider.GetRequiredService<IConfiguration>();
            var instance = Activator.CreateInstance<T>();
            SetValue.Apply(configuration, instance);

            // run any [SetValue] methods
            SetValues.ApplyAsync(instance).GetAwaiter().GetResult();

            // resolve the key vault secrets
            var httpClientFactory = provider.GetService<IHttpClientFactory>();
            var defaultAzureCredential = provider.GetService<DefaultAzureCredential>();
            ResolveSecret.ApplyAsync(instance, httpClientFactory, defaultAzureCredential).GetAwaiter().GetResult();

            // write to console
            WriteToConsole.Apply(configuration, instance);

            // validate
            var validationContext = new ValidationContext(instance, provider, null);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(instance, validationContext, validationResults, true))
            {
                foreach (var validationResult in validationResults)
                {
                    Console.WriteLine(validationResult.ErrorMessage);
                }
                Environment.Exit(1); // TODO: this probably needs to throw a custom exception
            }

            return instance;
        });

        // add AzureAppConfig as a service
        services.TryAddSingleton<AzureAppConfig>();

        // add the background service to show configurations
        services.AddHostedService<LogConfigBackgroundService>();

        return services;
    }

    /// <summary>
    /// Add the default Azure credential to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDefaultAzureCredential(this IServiceCollection services, bool logParams = true)
    {
        services.AddOptions<DefaultAzureCredentialOptions>().Configure<IConfiguration>((options, configuration) =>
        {
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
            if (logParams)
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
        });

        // add as a singleton
        services.TryAddSingleton<DefaultAzureCredential>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DefaultAzureCredentialOptions>>().Value;
            return new DefaultAzureCredential(options);
        });

        // add the background service to show configurations
        services.AddHostedService<LogConfigBackgroundService>();

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
        // add the options
        services.AddOptions<SingleLineConsoleLoggerOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.DISABLE_COLORS = configuration.GetValue<string>("DISABLE_COLORS").AsBool() ?? false;
            if (logParams)
            {
                Console.WriteLine("SingleLineConsoleLoggerOptions:");
                Console.WriteLine($"  DISABLE_COLORS = \"{options.DISABLE_COLORS}\"");
            }
        });

        // add the logger filter options
        services.AddOptions<LoggerFilterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.MinLevel = configuration.GetValue<string>("LOG_LEVEL").AsEnum<LogLevel>() ?? LogLevel.Information;
            if (logParams)
            {
                Console.WriteLine("LoggerFilterOptions:");
                Console.WriteLine($"  LOG_LEVEL = \"{options.MinLevel}\"");
            }
        });

        // add the logger
        services
            .AddLogging(configure =>
            {
                services.TryAddSingleton<ILoggerProvider, SingleLineConsoleLoggerProvider>();
            });

        // add the background service to show configurations
        services.AddHostedService<LogConfigBackgroundService>();

        return services;
    }
}