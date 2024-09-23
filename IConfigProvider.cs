namespace NetBricks;

/// <summary>
/// A configuration provider.
/// </summary>
public interface IConfigProvider
{
    /// <summary>
    /// Get the value of a key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    string Get(string key);
}
