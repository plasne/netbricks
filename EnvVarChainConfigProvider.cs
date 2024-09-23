namespace NetBricks;

/// <summary>
/// Get the value of the first environment variable that exists given a list of keys.
/// </summary>
public class EnvVarChainConfigProvider : IConfigProvider
{
    /// <summary>
    /// Get the value of the first environment variable that exists.
    /// </summary>
    /// <param name="keys">A string formatted </param>
    /// <returns></returns>
    public string Get(string keys)
    {
        var arr = keys.AsArray(() => new string[] { });
        foreach (var key in arr)
        {
            var val = System.Environment.GetEnvironmentVariable(key);
            if (val != null) return val;
        }
        return null;
    }
}