using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace NetBricks;

public class ConfigStartup<I> : IHostedService
    where I : class
{
    public ConfigStartup(IConfigFactory<I> configFactory)
    {
        this.configFactory = configFactory;
    }

    private readonly IConfigFactory<I> configFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = await this.configFactory.GetAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}