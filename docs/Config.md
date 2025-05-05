# Configuration Management

## Why?

I have a number of tenants I believe all configuration management solutions should have. I have tried to implement them in a way that is easy to use and understand.

- Configuration should be validated and conformed on startup. It is beneficial to terminate the execution of our service during startup when it has an invalid configuration, before it starts processing requests. When validating, the configuration should also be conformed to the expected datatypes and ranges. When the configuration value do not conform, it should fail validation or use a default.

- Configuration should be logged on startup. When troubleshooting issues with the service, it can be invaluable to look at the startup logs to see how the service was configured. While we do not want to show secrets, it is still important to show whether the value is set or not.

- Configuration should support secrets in a safe way. This solution should enable developers to store secrets easily and safely. It should make it easy to do the right thing.

- Configuration should be easy for administrators to set properly. There are several considerations here:

  - Configuration values should have reasonable defaults whenever possible.
  - Very specific configuration values may derive from more generic configuration values. For instance, if there are 4 places in the code that need a retry interval, consider using 4 configuration values that all default to using the value of a single configuration value.
  - Consider allowing for "templates" or "modes" that can be set as a single configuration value that sets many other configuration values.
  - Configuration values can interact with one another. For instance, if one value is true, other values may be required. These more complex validations should be enforced.
  - Configuration should be documented extensively and clearly.

## Usage

To do basic configuration management, create a class like this:

```csharp
using NetBricks;
using System.ComponentModel.DataAnnotations;

[LogConfig]
public class Config : IConfig
{
    [SetValue("NAME")]
    [Required]
    public string? NAME { get; set; }

    [SetValue("FAVORITE_NUMBER")]
    [Range(1, 100)]
    public int FAVORITE_NUMBER { get; set; } = 55;

    [SetValue("FAVORITE_ANIMALS")]
    [Required]
    public string[]? FAVORITE_ANIMALS { get; set; }
}
```

While not required, it it generally good practice to create an interface:

```csharp
public interface IConfig
{
    string? NAME { get; set; }
    int FAVORITE_NUMBER { get; set; }
    string[]? FAVORITE_ANIMALS { get; set; }
}
```

Then, you can add the configuration to the service collection like this:

```csharp
services.AddConfig<IConfig, Config>();
```

If not using an interface, you can also just:

```csharp
services.AddConfig<Config>();
```

## SetValueAttribute

The `SetValue` attribute can be applied to a Property. It will set the value of the property using `IConfiguration.GetValue()`. IConfiguration will be pulled from Dependency Injection. Commonly the values would be environment variables (which the dotnet configuration system is by default configured to use). To find out more about standard dotnet configuration, see [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration).

For a simple example:

```csharp
[SetValue("NAME")]
public string? NAME { get; set; }
```

This will set the value of the `NAME` property to the value of the environment variable `NAME`. If the environment variable is not set, it will be null.

For a more complex example:

```csharp
[SetValue("WEB_URL", "URL")]
public string? WEB_URL { get; set; }

[SetValue("API_URL", "URL")]
public string? API_URL { get; set; }
```

More than one value can be used and they will be tried in order. In this example, the user could set "URL" which would work for all the services or could set specific overrides for "WEB_URL" and/or "API_URL".

## SetValuesAttribute

The `SetValues` attribute can be applied to a Method. Any method with this attribute will be called after the property values are applied using SetValueAttribute.

One example, let's say you have a "mode" that sets a bunch of default values unless the user overrides, you could do something like this:

```csharp
using NetBricks;
using System;
using System.ComponentModel.DataAnnotations;

public class Config
{
    [SetValue("MODE")]
    [AllowedValues("API", "SERVER", "OTHER")]
    [Required]
    public string? MODE { get; set; }

    [SetValue("CONCURRENCY")]
    [Range(1, 100)]
    public int CONCURRENCY { get; set; }

    [SetValues]
    public void ApplyMode()
    {
        if (CONCURRENCY > 0) return;
        if (MODE == "API")
        {
            CONCURRENCY = 5;
        }
        else if (MODE == "SERVER")
        {
            CONCURRENCY = 10;
        }
        else
        {
            CONCURRENCY = 1;
        }
    }
}
```

If CONCURRENCY was set to something greater than 0 by the SetValueAttribute, it would not be changed. Otherwise, it would be set to 5 for API, 10 for SERVER, and 1 for OTHER.

SetValuesAttribute has an order property that can be used to control the order in which the methods are called. The default is 0. The lower the number, the earlier it will be called. This is useful if you have multiple methods that need to be called in a specific order (ex. `[SetValues(order: 0)]`, `[SetValues(order: 1)]`, etc.).

## LogConfigAttribute

The `LogConfig` attribute can be applied to a Class or Property. When applied to a Class, it will write the entire class to the ILogger (default) or console. When applied to a Property, it will write just that property. The settings of a Property override the settings of the Class.

There are modes that can be used to control what and how the configuration is written. The modes are:

- `Always`: Write the name of the property and the value. This is the default.
- `Never`: Do not write the name and value.
- `IfNotEmpty`: Write the name of the property and the value to the ILogger or console provided the value is not empty or null.
- `Masked` - Write the name of the property, the value will be `**MASKED**`.

For example, to print all the values except a secret:

```csharp
[LogConfig(LogConfigMode.Always)]
public class Config
{
    public string? NAME { get; set; }

    [LogConfig(LogConfigMode.Masked)]
    public string? SECRET { get; set; }
}
```

```text
NAME = "Peter"
SECRET = "**MASKED**"
```

You can also apply a header to the class, which will be printed before the configuration is printed and indent the properties. This makes it easier to read:

```csharp
[LogConfig("Application:")]
public class Config
{ }
```

```text
Application:
  NAME = "Peter"
  FAVORITE_NUMBER = "55"
  FAVORITE_COLOR = "Blue"
```

You can even add headers to properties:

```csharp
[LogConfig("->", "FAVORITE_COLOR")]
public string? FAVORITE_COLOR { get; set; }
```

```text
Application:
  NAME = "Peter"
  FAVORITE_NUMBER = "55"
->FAVORITE_COLOR = "Blue"
```

## ResolveSecretAttribute

The `ResolveSecret` attribute can be applied to a Property. It will resolve the value of the property using Azure Key Vault. The value of the property must be a URL to a secret in Azure Key Vault.

For example, SECRET might be set to a URL like <https://myvault.vault.azure.net/secrets/mysecret>. That secret would be read from Azure Key Vault and the value of the property would be set to the value of that secret. The secret would be masked when printed to the console.

```csharp
[SetValue("SECRET")]
[ResolveSecret]
[LogConfig(LogConfigMode.Masked)]
public string? SECRET { get; set; }
```

In order for this resolution to work, you must include the following in your service collection:

```csharp
services.AddHttpClient();
services.AddDefaultAzureCredential();
```

The resolution will use the named HTTP client of "netbricks" in case you want to configure the HTTP client differently. Adding the DefaultAzureCredential gives the permissions required to access the Key Vault.

IMPORTANT: Currently the ResolveSecret attribute only works if the type is `string` (or `string?`).

## Validations

Data Annotations are used to validate the configuration values. In addition, custom validations could be created using the same framework.

The best write-up I have seen on Data Annotations is here: [Data Annotations Validation](https://weblogs.asp.net/ricardoperes/net-8-data-annotations-validation).

Sometimes it is necessary to validate the entire configuration object instead of individual properties. This can be done by implementing the `IValidatableObject` interface. This interface has a single method, `Validate`, that is called after all the properties have been set. This is useful for validating complex scenarios where multiple properties are related to one another. For example:

```csharp
using NetBricks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class Config : IValidatableObject
{
    [SetValue("MODE")]
    [AllowedValues("API", "SERVER", "OTHER")]
    [Required]
    public string? MODE { get; set; }

    [SetValue("CONCURRENCY")]
    public int CONCURRENCY { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MODE == "API" && CONCURRENCY < 5)
        {
            yield return new ValidationResult("CONCURRENCY must be at least 5 for API mode.", new[] { nameof(CONCURRENCY) });
        }
        else if (MODE == "SERVER" && CONCURRENCY < 10)
        {
            yield return new ValidationResult("CONCURRENCY must be at least 10 for SERVER mode.", new[] { nameof(CONCURRENCY) });
        }
    }
}
```

## Comparison

I have evaluated a number of configuration management solutions and have found that most of them do not meet all of these requirements or don't cover the same scope as this solution.
