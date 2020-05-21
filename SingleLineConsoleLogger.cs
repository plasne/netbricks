using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NetBricks
{

    public static class AddSingleLineConsoleLoggerConfiguration
    {

        public static void AddSingleLineConsoleLogger(this IServiceCollection services, bool logParams = true)
        {

            // log the logger variables
            if (logParams)
            {
                Console.WriteLine($"LOG_LEVEL = \"{Config.LogLevel}\", colors? {!Config.DisableColors}");
            }

            // add the logger
            services
                .AddLogging(configure =>
                {
                    services.TryAddSingleton<ILoggerProvider, SingleLineConsoleLoggerProvider>();
                })
                .Configure<LoggerFilterOptions>(options =>
                {
                    options.MinLevel = Config.LogLevel;
                });

        }

    }


    public class SingleLineConsoleLoggerProvider : ILoggerProvider
    {

        private ConcurrentDictionary<string, SingleLineConsoleLogger> Loggers = new ConcurrentDictionary<string, SingleLineConsoleLogger>();

        public ILogger CreateLogger(string categoryName)
        {
            return Loggers.GetOrAdd(categoryName, name => new SingleLineConsoleLogger(name, Config.DisableColors));
        }

        public void Dispose()
        {
            foreach (var logger in Loggers)
            {
                logger.Value.Shutdown();
            }
            Loggers.Clear();
        }
    }


    public class SingleLineConsoleLogger : ILogger
    {

        public SingleLineConsoleLogger(string name, bool disableColors)
        {
            this.Name = name;
            this.DisableColors = disableColors;
            Dispatcher = Task.Run(() =>
            {
                while (!QueueTakeCts.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine(Queue.Take(QueueTakeCts.Token));
                    }
                    catch (OperationCanceledException)
                    {
                        // let the loop end
                    }
                }
                IsShutdown.Set();
            });
        }

        private string Name { get; }
        private bool DisableColors { get; }
        private BlockingCollection<string> Queue { get; set; } = new BlockingCollection<string>();
        private CancellationTokenSource QueueTakeCts { get; set; } = new CancellationTokenSource();
        private Task Dispatcher { get; set; }
        private ManualResetEventSlim IsShutdown { get; set; } = new ManualResetEventSlim(false);

        public void Shutdown()
        {
            QueueTakeCts.Cancel();
            IsShutdown.Wait(5000);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
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
                sb.Append($" {DateTime.UtcNow.ToString()} [src:{Name}] ");
                sb.Append(message);
                Queue.Add(sb.ToString());
            }

            // write the exception
            if (exception != null)
            {
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
            public ConsoleColors(string foreground, string background)
            {
                Foreground = foreground;
                Background = background;
            }

            public string Foreground { get; }

            public string Background { get; }
        }

    }

}
