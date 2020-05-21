# Why NetBricks?

I found myself using a lot of the same code and techniques from project-to-project, however, I was always copying the code and then it would deviate. I decided instead to take the best components from all my recent solutions and put the base level components into something I am calling NetBricks.

Included:

-   Config
-   AccessTokenFetcher
-   SingleLineConsoleLogger

## Services

The entire solution depends on Dependency Injection. If you want everything, you should inject the following services...

```c#
services.AddHttpClient("netbricks");
services.AddSingleLineConsoleLogger();
services.AddAccessTokenFetcher();
services.AddConfig();
```

## Config

This is a flexible and efficient configuration management solution that supports the following features:

-   values can be pulled from environment variables
-   values can be pulled from Azure App Configuration
-   values that are URLs can be resolved to secrets in Azure Key Vault
-   values can be cached
-   values can be converted to the appropriate datatype
-   defaults can be set

### GetOnce()

If you need access to a value, don't need to resolve in Key Vault, and don't need to cache, use GetOnce(). You might also consider doing it as "static" since it has no requirement for an instance.

```c#
public static LogLevel LogLevel
{
    get => GetOnce("APP_LOG_LEVEL", "LOG_LEVEL").AsEnum<LogLevel>(() => LogLevel.Information);
}
```

More to come...
