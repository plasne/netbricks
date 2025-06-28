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
        ILogger<OptionsStartup> logger,
        IOptionsMonitor<LoggerFilterOptions> loggerFilterOptionsMonitor,
        SingleLineConsoleLoggerOptions? singleLineConsoleLoggerOptions = null,
        DefaultAzureCredentialOptions? defaultAzureCredentialOptions = null,
        AzureAppConfigOptions? configOptions = null)
    {
        if (singleLineConsoleLoggerOptions is not null && singleLineConsoleLoggerOptions.LOG_TO_CONSOLE)
        {
            if (loggerFilterOptionsMonitor.CurrentValue is not null)
            {
                logger.LogInformation($"LOG_LEVEL = \"{loggerFilterOptionsMonitor.CurrentValue.MinLevel}\"");
            }
            logger.LogInformation($"LOG_WITH_COLORS = \"{singleLineConsoleLoggerOptions.LOG_WITH_COLORS}\"");
        }
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