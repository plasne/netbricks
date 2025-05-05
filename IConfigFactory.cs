using System.Threading;
using System.Threading.Tasks;

namespace NetBricks;

/// <summary>
/// Interface for ConfigFactory that provides configuration objects.
/// </summary>
/// <typeparam name="I">The interface type of the configuration object</typeparam>
public interface IConfigFactory<I>
    where I : class
{
    /// <summary>
    /// Gets the configuration object while resolving all settings from Azure AppConfig, Key Vault, etc.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation</param>
    /// <returns>A configured instance of the configuration object</returns>
    Task<I> GetAsync(CancellationToken cancellationToken = default);
}