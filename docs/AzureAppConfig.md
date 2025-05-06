# Azure App Configuration

When dealing with dozens, hundreds, or even thousands of configuration settings across multiple environments, it can be hard to keep track of them all. Azure App Configuration is a service that allows you to store and manage your application settings in one place.

This capability is designed to read settings from Azure App Configuration and make them available as environment variables. This load and apply is done before the configuration management system is applied.

## Usage

You can add it to the service collection like this:

```csharp
services.AddHttpClient(); // pre-requisite
services.AddDefaultAzureCredential(); // pre-requisite
services.AddAzureAppConfig();
```

Make sure you have set both `APPCONFIG_URL` and `APPCONFIG_KEYS` environment variables as per configuration below.

## Configuration

The following configuration options are available:

- `APPCONFIG_URL`: The URL to the Azure App Configuration instance. This is required.

- `APPCONFIG_KEYS`: This is a comma-delimited list of configuration keys to pull for the specific service. All keys matching the pattern will be pulled. A setting that is already set is not replaced (so left-most patterns take precident). For example, the dev environment of the auth service might contain "app:auth:dev:\*, app:common:dev:\*". In this example, "auth" settings would precident over "common" settings. This is required.

- `APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS`: This defaults to false, which indicates that only the last section of the key is used as the environment variable. If set to true, the entire key is used as the environment variable. For instance, consider "app:auth:dev:AUTHORITY_URL" and "app:common:dev:AUTHORITY_URL", if fully qualified keys are used the environment variable would be "app:auth:dev:AUTHORITY_URL" and "app:common:dev:AUTHORITY_URL". If not, the environment variable would be "AUTHORITY_URL" and the first one set would take precident.
