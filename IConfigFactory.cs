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
    /// Gets the configuration object, creating and configuring it if necessary.
    /// </summary>
    /// <returns>A configured instance of the configuration object</returns>
    Task<I> GetAsync();
}