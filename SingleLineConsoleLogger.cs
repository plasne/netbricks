using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace NetBricks;

public class SingleLineConsoleLoggerProvider : ILoggerProvider
{
    public SingleLineConsoleLoggerProvider(IOptions<SingleLineConsoleLoggerOptions> options)
    {
        this.SingleLineConsoleLoggerOptions = options.Value;
    }

    private SingleLineConsoleLoggerOptions SingleLineConsoleLoggerOptions { get; }
    private ConcurrentDictionary<string, SingleLineConsoleLogger> Loggers = new ConcurrentDictionary<string, SingleLineConsoleLogger>();
    private bool disposed;
    private readonly object disposeLock = new object();

    public ILogger CreateLogger(string categoryName)
    {
        return Loggers.GetOrAdd(categoryName, name => new SingleLineConsoleLogger(name, SingleLineConsoleLoggerOptions));
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (this.disposeLock)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    foreach (var logger in Loggers)
                    {
                        logger.Value.Dispose();
                    }
                    Loggers.Clear();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                this.disposed = true;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class SingleLineConsoleLogger : ILogger, IDisposable
{
    public SingleLineConsoleLogger(string name, SingleLineConsoleLoggerOptions options)
    {
        this.Name = name;
        this.DisableColors = options.DISABLE_COLORS;

        // create an unbounded channel for the log messages
        LogChannel = System.Threading.Channels.Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true, // only one reader will be reading from the channel
            AllowSynchronousContinuations = false // process continuations asynchronously
        });

        // start the dispatcher task
        Dispatcher = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in LogChannel.Reader.ReadAllAsync(CancellationToken))
                {
                    Console.WriteLine(message);
                }
                IsShutdown.Set();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SingleLineConsoleLogger Dispatcher: {ex}");
            }
        });
    }

    private string Name { get; }
    private bool DisableColors { get; }
    private Channel<string> LogChannel { get; }
    private CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    private CancellationToken CancellationToken => CancellationTokenSource.Token;
    private Task Dispatcher { get; }
    private ManualResetEventSlim IsShutdown { get; } = new ManualResetEventSlim(false);

    private bool disposedValue;

    private readonly Lazy<bool> channelErrorReported = new Lazy<bool>(() =>
    {
        Console.WriteLine("SingleLineConsoleLogger: Channel is full, writing to console directly.");
        return true;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        // write the message
        var message = formatter(state, exception);
        if (!string.IsNullOrEmpty(message))
        {
            // write the message
            var sb = new StringBuilder();
            var logLevelColors = GetLogLevelConsoleColors(logLevel);
            if (!DisableColors && logLevelColors.Foreground != null) sb.Append(logLevelColors.Foreground);
            if (!DisableColors && logLevelColors.Background != null) sb.Append(logLevelColors.Background);
            var logLevelString = GetLogLevelString(logLevel);
            sb.Append(logLevelString);
            if (!DisableColors) sb.Append("\u001b[0m"); // reset
            sb.Append($" {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [src:{Name}] ");
            sb.Append(message);

            // the channel should only be full if the system is out of memory
            if (!LogChannel.Writer.TryWrite(sb.ToString()))
            {
                _ = this.channelErrorReported.Value;
                Console.WriteLine(message);
            }
        }

        // write the exception
        if (exception != null)
        {
            // For exceptions, we want to ensure they appear in the log
            // We could also put this in the channel, but direct console output
            // ensures it appears immediately even if the channel is backed up
            Console.WriteLine(exception.ToString());
        }
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
                return "trce";
            case LogLevel.Debug:
                return "dbug";
            case LogLevel.Information:
                return "info";
            case LogLevel.Warning:
                return "warn";
            case LogLevel.Error:
                return "fail";
            case LogLevel.Critical:
                return "crit";
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel));
        }
    }

    private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
    {
        if (DisableColors) return new ConsoleColors(null, null);

        // We must explicitly set the background color if we are setting the foreground color,
        // since just setting one can look bad on the users console.
        switch (logLevel)
        {
            case LogLevel.Critical:
                return new ConsoleColors("\u001b[37m", "\u001b[41m"); // white on red
            case LogLevel.Error:
                return new ConsoleColors("\u001b[30m", "\u001b[41m"); // black on red
            case LogLevel.Warning:
                return new ConsoleColors("\u001b[33m", "\u001b[40m"); // yellow on black
            case LogLevel.Information:
                return new ConsoleColors("\u001b[32m", "\u001b[40m"); // green on black
            case LogLevel.Debug:
                return new ConsoleColors("\u001b[37m", "\u001b[40m"); // white on black
            case LogLevel.Trace:
                return new ConsoleColors("\u001b[37m", "\u001b[40m"); // white on black
            default:
                return new ConsoleColors(null, null);
        }
    }

    private readonly struct ConsoleColors
    {
        public ConsoleColors(string? foreground, string? background)
        {
            Foreground = foreground;
            Background = background;
        }

        public string? Foreground { get; }

        public string? Background { get; }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                try
                {
                    LogChannel.Writer.Complete();
                    CancellationTokenSource.Cancel();
                    IsShutdown.Wait(5000);
                }
                finally
                {
                    CancellationTokenSource.Dispose();
                    IsShutdown.Dispose();
                }
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            this.disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
}
