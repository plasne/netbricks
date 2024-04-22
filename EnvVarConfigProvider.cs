namespace NetBricks;

public class EnvVarConfigProvider : IConfigProvider
{
    public string Get(string key)
    {
        return System.Environment.GetEnvironmentVariable(key);
    }
}