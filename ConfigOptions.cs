namespace NetBricks;

/// <summary>
/// Configuration options for Azure App Configuration integration.
/// These settings control how keys are loaded from Azure App Configuration
/// and how they are processed into environment variables.
/// </summary>
public class ConfigOptions
{
    /// <summary>
    /// The URL of the Azure App Configuration instance.
    /// If not provided with protocol or domain, will automatically add https:// and .azconfig.io.
    /// </summary>
    public string? APPCONFIG_URL { get; set; }

    /// <summary>
    /// Array of key filters to retrieve from Azure App Configuration.
    /// Each entry can be a specific key or a key pattern with wildcards.
    /// </summary>
    public string[]? APPCONFIG_KEYS { get; set; }

    /// <summary>
    /// When true, uses the full key name including namespaces (e.g. "AppName:Component:Setting").
    /// When false, uses only the last segment of the key (e.g. "Setting").
    /// Default is false.
    /// </summary>
    public bool APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS { get; set; }
}