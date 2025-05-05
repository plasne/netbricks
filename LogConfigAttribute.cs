using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NetBricks;

/// <summary>
/// Controls when property values are written to the console
/// </summary>
public enum LogConfigMode
{
    /// <summary>
    /// Never write the property value to the console
    /// </summary>
    Never,

    /// <summary>
    /// Always write the property value to the console
    /// </summary>
    Always,

    /// <summary>
    /// Only write the property value to the console if it is not null or empty
    /// </summary>
    IfNotEmpty,

    /// <summary>
    /// Write the property value to the console but mask it with "**MASKED**"
    /// </summary>
    Masked
}

/// <summary>
/// Attribute to control logging of configuration properties.
/// </summary>
/// <remarks>
/// This attribute can be applied to classes and properties to control when their values are logged.
/// The logging behavior is determined by the <see cref="LogConfigMode"/> enum.
/// The attribute can be applied to classes and properties.
/// If applied to a class, it will apply to all properties of that class.
/// If applied to a property, it will override the class-level attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class LogConfigAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the header to be used when logging the property value.
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the logging mode for the property.
    /// </summary>
    public LogConfigMode Mode { get; set; } = LogConfigMode.Always;

    public LogConfigAttribute(string? header = null, LogConfigMode mode = LogConfigMode.Always)
    {
        this.Header = header;
        this.Mode = mode;
    }
}

internal static class LogConfig
{
    internal static void Apply<T>(IConfiguration configuration, T instance, ILogger? logger) where T : class
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var classAttribute = Attribute.GetCustomAttribute(typeof(T), typeof(LogConfigAttribute)) as LogConfigAttribute;
        if (classAttribute is not null && !string.IsNullOrEmpty(classAttribute.Header) && logger is null)
        {
            Console.WriteLine(classAttribute.Header);
        }

        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            // Check if the property has the WriteToConsole attribute
            var propertyAttribute = Attribute.GetCustomAttribute(property, typeof(LogConfigAttribute)) as LogConfigAttribute;

            // Determine which mode to use (property attribute takes precedence over class attribute)
            LogConfigMode mode = LogConfigMode.Always;
            if (propertyAttribute is not null)
            {
                mode = propertyAttribute.Mode;
            }
            else if (classAttribute is not null)
            {
                mode = classAttribute.Mode;
            }

            // If mode is Never, skip this property
            if (mode == LogConfigMode.Never)
                continue;

            // Get the target type
            var targetType = property.PropertyType;
            var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableUnderlyingType ?? targetType;

            // Get the value
            var value = property.GetValue(instance);
            var stringValue = AsString(effectiveType, value);

            // Check if we should skip empty values
            if (mode == LogConfigMode.IfNotEmpty && string.IsNullOrEmpty(stringValue))
                continue;

            // Get the prefix and apply mask
            var prefix = Prefix(logger, classAttribute?.Header, propertyAttribute?.Header);
            if (mode == LogConfigMode.Masked) stringValue = "**MASKED**";

            // Log the value
            if (logger is not null)
            {
                logger.LogInformation($"{prefix}{property.Name} = \"{stringValue}\"");
            }
            else
            {
                Console.WriteLine($"{prefix}{property.Name} = \"{stringValue}\"");
            }
        }
    }

    internal static string? Prefix(ILogger? logger, string? classHeader, string? propertyHeader)
    {
        if (logger is not null)
        {
            if (!string.IsNullOrEmpty(propertyHeader))
                return propertyHeader;
            if (!string.IsNullOrEmpty(classHeader))
                return classHeader;
        }

        if (!string.IsNullOrEmpty(propertyHeader))
            return propertyHeader;
        if (!string.IsNullOrEmpty(classHeader))
            return "  ";

        return string.Empty;
    }

    internal static string? AsString(Type? type, object? value)
    {
        if (value == null)
            return null;
        if (type == typeof(string[]))
            return string.Join(", ", value as string[] ?? []);
        if (type == typeof(IEnumerable<string>))
            return string.Join(", ", value as IEnumerable<string> ?? []);
        if (type == typeof(IList<string>))
            return string.Join(", ", value as IList<string> ?? []);
        if (type == typeof(List<string>))
            return string.Join(", ", value as List<string> ?? []);
        return value.ToString();
    }
}