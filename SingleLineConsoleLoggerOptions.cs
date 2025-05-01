using Microsoft.Extensions.Logging;

namespace NetBricks;

public class SingleLineConsoleLoggerOptions
{
    public required LogLevel LOG_LEVEL { get; set; }
    public required bool DISABLE_COLORS { get; set; }
}