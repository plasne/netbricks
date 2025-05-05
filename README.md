# NetBricks

I found myself using a lot of the same code and techniques from project-to-project, however, I was always copying the code and then it would deviate. I decided instead to take the best components from all my recent solutions and put the base level components into something I am calling NetBricks.

Included (click on the links to see the documentation):

- [SingleLineConsoleLogger](./docs/SingleLineConsoleLogger.md): A high performance console logger that will print out all log messages on a single line.

- [DefaultAzureCredential](./docs/DefaultAzureCredential.md): While a standard Microsoft library, I have included a wrapper that will allow you to configure the DefaultAzureCredential in an easy way.

- [AzureAppConfig](./docs/AzureAppConfig.md): A wrapper around the Azure App Configuration library that brings in hierarchical configuration as environment variables. This can be used in conjunction with the Configuration Management capabilities.

- [Configuration Management](./docs/Config.md): A configuration management solution that allows you to pull values from environment variables, Azure App Configuration, and Azure Key Vault. It will enforce and conform values as needed before printing them out at startup.

- [Extensions](./docs/Extensions.md): A collection of extensions for conforming strings to various datatypes. These are used in the configuration management solution.

## Services

The entire solution depends on Dependency Injection. If you want everything, you should inject the following services...

```csharp
services.AddSingleLineConsoleLogger();
services.AddDefaultAzureCredential();
services.AddAzureAppConfig();
services.AddConfig<I, T>();
```

All of these add methods have a property called `logMethod` which can be set to `ILogger` (default) or `Console`. When set to `ILogger`, the settings used by this component will be logged to the ILogger, otherwise those settings will be printed directly to the console.
