# SingleLineConsoleLogger

## Why?

Calling `Console.WriteLine` is slow. It is a blocking call and can cause performance issues if you are logging a lot of messages. This logger will print out all log messages on a single line and is much, much faster.

Alternatives: Some logging libraries such as NLog and Serilog implement asynchronous logging in a similar fashion.

## Usage

You can add it to the service collection like this:

```csharp
services.AddSingleLineConsoleLogger();
```

You might also consider removing other loggers. Here are some examples of how to do that:

```csharp
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.Services.AddSingleLineConsoleLogger();
});
```

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.Services.AddSingleLineConsoleLogger();
    })
    .Build();
```

## Configuration

The following configuration options are available:

- `LOG_LEVEL`: The minimum log level to log. Default is `Information`. These are the same as the Microsoft.Extensions.Logging.LogLevel enum values.

- `LOG_WITH_COLORS`: If set to `true`, the logger will use colors. Default is `true`.
