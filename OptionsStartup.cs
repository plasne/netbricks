using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetBricks;

internal class OptionsStartup : IHostedService
{
    public OptionsStartup(
        SingleLineConsoleLoggerOptions? singleLineConsoleLoggerOptions = null,
        DefaultAzureCredentialOptions? defaultAzureCredentialOptions = null,
        AzureAppConfigOptions? configOptions = null)
    {
        // NOTE: we don't use anything from the above, but we need them to be provisioned by the DI container
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}