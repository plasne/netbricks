using System.Threading.Tasks;

namespace NetBricks;

/// <summary>
/// Configuration options for Azure App Configuration integration.
/// These settings control how keys are loaded from Azure App Configuration
/// and how they are processed into environment variables.
/// </summary>
internal class AzureAppConfigOptions
{
    /// <summary>
    /// The URL of the Azure App Configuration instance.
    /// If not provided with protocol or domain, will automatically add https:// and .azconfig.io.
    /// </summary>
    internal string? APPCONFIG_URL { get; set; }

    /// <summary>
    /// Array of key filters to retrieve from Azure App Configuration.
    /// Each entry can be a specific key or a key pattern with wildcards.
    /// </summary>
    internal string[]? APPCONFIG_KEYS { get; set; }

    /// <summary>
    /// When true, uses the full key name including namespaces (e.g. "AppName:Component:Setting").
    /// When false, uses only the last segment of the key (e.g. "Setting").
    /// Default is false.
    /// </summary>
    internal bool APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS { get; set; }

    /// <summary>
    /// This TaskCompletionSource is used to signal when the Azure App Configuration has been loaded.
    /// This ensures that the Config system doesn't do any work until the Azure App Configuration
    /// has been loaded and the keys have been set.
    /// </summary>
    internal TaskCompletionSource WaitForLoad { get; } = new TaskCompletionSource();
}