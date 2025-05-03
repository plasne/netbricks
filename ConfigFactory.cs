using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

public class ConfigFactory<I, T> : IConfigFactory<I>
    where I : class
    where T : class, I
{
    public ConfigFactory(
        IServiceProvider provider,
        AzureAppConfig azureAppConfig,
        IConfiguration configuration,
        IHttpClientFactory? httpClientFactory = null,
        DefaultAzureCredential? defaultAzureCredential = null)
    {
        this.provider = provider;
        this.azureAppConfig = azureAppConfig;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
        this.defaultAzureCredential = defaultAzureCredential;
    }

    private readonly IServiceProvider provider;
    private readonly AzureAppConfig azureAppConfig;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory? httpClientFactory;
    private readonly DefaultAzureCredential? defaultAzureCredential;
    private readonly SemaphoreSlim getLock = new(1, 1);
    private T? config;

    public async Task<I> GetAsync()
    {
        if (this.config is not null)
            return this.config;

        await this.getLock.WaitAsync();
        try
        {
            if (this.config is not null)
                return this.config;

            // load Azure App Config
            await azureAppConfig.LoadAsync();

            // set the values on the config object
            var instance = Activator.CreateInstance<T>();
            SetValue.Apply(configuration, instance);

            // run any [SetValues] methods
            await SetValues.ApplyAsync(instance);

            // resolve the key vault secrets
            await ResolveSecret.ApplyAsync(instance, httpClientFactory, defaultAzureCredential);

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

            this.config = instance;
            return instance;
        }
        finally
        {
            this.getLock.Release();
        }
    }
}