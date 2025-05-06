# DefaultAzureCredential

## Why?

The `DefaultAzureCredential` is a standard Microsoft library that allows you to authenticate to Azure services using a variety of methods. However, by default, it can take a long time to get tokens because it will try all the available methods in order. Each of those methods can take a long time to fail and so it is not uncommon for tokens to take tens or hundreds of seconds to get a token.

## Usage

You can add it to the service collection like this:

```csharp
services.AddDefaultAzureCredential();
```

If you provide no configuration, the solution will look at the ASPNETCORE_ENVIRONMENT environment variable. If it is set to "Development", the DefaultAzureCredential will use "mi" (Managed Identity) and then "azcli" (Azure CLI) to try and get a token. Otherwise, it will use "env" (Environment Variables) and then "mi" (Managed Identity) to try and get a token. These options are very fast and will work for most scenarios, but you can specify which methods are allowed by setting the INCLUDE_CREDENTIALS_TYPE environment variable. The options are:

- `env` - Environment Variables
- `mi` - Managed Identity
- `token` - Shared Token Cache
- `vs` - Visual Studio Credential
- `vscode` - Visual Studio Code Credential
- `azcli` - Azure CLI
- `browser` - Interactive Browser
- `azd` - Azure Developer CLI
- `ps` - Azure PowerShell
- `workload` - Workload Identity

For example,

```dotenv
INCLUDE_CREDENTIALS_TYPE=env,mi,azcli,azd
```

Ideally, set only the specific method you wish to use. For instance, a deployed service could specify:

```dotenv
INCLUDE_CREDENTIALS_TYPE=mi
```

When using Managed Identity, you will often need to set the `AZURE_CLIENT_ID` environment variable to the client ID of the Managed Identity.
