using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace NetBricks;

internal class ConfigStartup<I> : IHostedService
    where I : class
{
    public ConfigStartup(IConfigFactory<I> configFactory, LogMethod logMethod)
    {
        this.configFactory = configFactory;
        this.logMethod = logMethod;
    }

    private readonly IConfigFactory<I> configFactory;
    private readonly LogMethod logMethod;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = await this.configFactory.GetAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}