namespace NetBricks;

/// <summary>
/// A configuration provider that reads from environment variables.
/// </summary>
public class EnvVarConfigProvider : IConfigProvider
{
    /// <summary>
    /// Get the value of an environment variable.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string Get(string key)
    {
        return System.Environment.GetEnvironmentVariable(key);
    }
}