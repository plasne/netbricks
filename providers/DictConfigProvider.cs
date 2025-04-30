using System.Collections.Generic;

namespace NetBricks;

/// <summary>
/// Use a dictionary as a configuration provider.
/// </summary>
public class DictConfigProvider : IConfigProvider
{
    /// <summary>
    /// The dictionary of values.
    /// </summary>
    private Dictionary<string, string> values = new Dictionary<string, string>();

    /// <summary>
    /// Get the value of a key from the dictionary.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string Get(string key)
    {
        return this.values.ContainsKey(key) ? this.values[key] : null;
    }

    /// <summary>
    /// Add a key-value pair to the dictionary.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Add(string key, string value)
    {
        this.values[key] = value;
    }
}