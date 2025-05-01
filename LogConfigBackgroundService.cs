using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NetBricks;

public class LogConfigBackgroundService : BackgroundService
{
    public LogConfigBackgroundService(
        IOptions<SingleLineConsoleLoggerOptions> singleLineConsoleLoggerOptions,
        IOptions<DefaultAzureCredentialOptions> defaultAzureCredentialOptions,
        IOptions<ConfigOptions> configOptions)
    {
        this.singleLineConsoleLoggerOptions = singleLineConsoleLoggerOptions;
        this.defaultAzureCredentialOptions = defaultAzureCredentialOptions;
        this.configOptions = configOptions;
    }

    private readonly IOptions<SingleLineConsoleLoggerOptions> singleLineConsoleLoggerOptions;
    private readonly IOptions<DefaultAzureCredentialOptions> defaultAzureCredentialOptions;
    private readonly IOptions<ConfigOptions> configOptions;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = this.singleLineConsoleLoggerOptions.Value;
        _ = this.defaultAzureCredentialOptions.Value;
        _ = this.configOptions.Value;
        return Task.CompletedTask;
    }
}