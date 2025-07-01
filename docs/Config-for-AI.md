# Implementing Netbricks Configuration Management

## DO

- Use uppercase characters and underscores for all configuration keys consistent with environment variables.

- Implement an interface for all configuration.

  ```csharp
  // example
  public interface IConfig
  {
      string? FAVORITE_COLOR { get; set; }
  }
  ```

- Implement a concrete class for the configuration that implements the interface.

  ```csharp
  // example
  [LogConfig]
  public class Config : IConfig
  {
    [SetValue("FAVORITE_COLOR")]
    public string? FAVORITE_COLOR { get; set; }
  }
  ```

- Use a single configuration class for all settings.

- Register the configuration in the service collection using the `AddConfig<T>()` method.

  ```csharp
  // example
  services.AddConfig<IConfig, Config>();
  ```

- Ensure configuration is retrieved using the `IConfigFactory<T>` interface.

  ```csharp
  // example
  public class MyService
  {
    private readonly IConfigFactory<IConfig> configFactory;

    public MyService(IConfigFactory<IConfig> configFactory)
    {
        this.configFactory = configFactory;
    }

    public async Task DoSomethingAsync()
    {
        var config = await configFactory.GetAsync();
        var favoriteColor = config.FAVORITE_COLOR;
        // Use the configuration value...
    }
  }
  ```

- Use the `dotenv.net` library to load environment variables from a `.env` file.

- Ensure all sensitive information uses the `[ResolveSecret]` attribute (allows for retrieval from an Azure Key Vault) and the `[LogConfig(mode: LogConfigMode.Masked)]` attribute (ensures the secret is not exposed in the logs).

- Ensure unset parameters should default to null or their default values, not empty strings.

- Use the `[SetValue]` attribute to map environment variables to properties in the configuration class. The following data types are supported:

  - `string`
  - `bool`
  - `int`
  - `long`
  - `float`
  - `double`
  - `Guid`
  - Enums
  - `string[]` or `IEnumerable<string>`
  - `List<string>` or `IList<string>`

- Use the `[Required]` attribute to ensure that required parameters are set.

- `System.ComponentModel.DataAnnotations` attributes can be used to validate the configuration properties further.

- Consult with the [full documentation](https://github.com/plasne/netbricks/blob/master/docs/Config.md) if this guide does not cover a use-case you are trying to implement. Some other use-cases include:

  - Using Azure App Configuration
  - Using Azure Key Vault
  - Setting a variable based on other variables
  - Validation spanning multiple variables

## DO NOT

- Do not implement a health check for configuration management. Instead, ensure that the configuration is validated at startup.

- Do not attempt to get the configuration object directly from the service provider without using the `IConfigFactory<T>` interface.

- Do not attempt to do a conversion of a datatype manually if that is already supported by `[SetValue]`.
