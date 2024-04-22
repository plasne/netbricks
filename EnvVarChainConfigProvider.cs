namespace NetBricks;

public class EnvVarChainConfigProvider : IConfigProvider
{
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