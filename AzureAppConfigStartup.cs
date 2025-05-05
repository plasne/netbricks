using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.Extensions.Hosting;

namespace NetBricks;

internal class AzureAppConfigStartup : IHostedService
{
    public AzureAppConfigStartup(AzureAppConfigOptions options, AzureAppConfig config)
    {
        this.options = options;
        this.config = config;
    }

    private readonly AzureAppConfigOptions options;
    private readonly AzureAppConfig config;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.config.LoadAsync(cancellationToken);
        }
        finally
        {
            this.options.WaitForLoad.SetResult();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}