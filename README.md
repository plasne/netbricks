# NetBricks

I found myself using a lot of the same code and techniques from project-to-project, however, I was always copying the code and then it would deviate. I decided instead to take the best components from all my recent solutions and put the base level components into something I am calling NetBricks.

Included (click on the links to see the documentation):

- [SingleLineConsoleLogger](./docs/SingleLineConsoleLogger.md): A high performance console logger that will print out all log messages on a single line.

- [DefaultAzureCredential](./docs/DefaultAzureCredential.md): While a standard Microsoft library, I have included a wrapper that will allow you to configure the DefaultAzureCredential in an easy way.

- [Configuration Management](./docs/Config.md): A configuration management solution that allows you to pull values from environment variables, Azure App Configuration, and Azure Key Vault. It will enforce and conform values as needed before printing them out at startup.

## Services

The entire solution depends on Dependency Injection. If you want everything, you should inject the following services...

```csharp
services.AddSingleLineConsoleLogger();
services.AddDefaultAzureCredential();
services.AddHttpClientForConfig();
services.AddConfig();
```
