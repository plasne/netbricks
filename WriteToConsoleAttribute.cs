using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

/// <summary>
/// Controls when property values are written to the console
/// </summary>
public enum WriteToConsoleMode
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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class WriteToConsoleAttribute : Attribute
{
    public string? Header { get; set; }
    public WriteToConsoleMode Mode { get; set; } = WriteToConsoleMode.Always;

    public WriteToConsoleAttribute(string? header = null, WriteToConsoleMode mode = WriteToConsoleMode.Always)
    {
        this.Header = header;
        this.Mode = mode;
    }
}

internal static class WriteToConsole
{
    internal static void Apply<T>(IConfiguration configuration, T instance) where T : class
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        string indentation = string.Empty;
        var classAttribute = Attribute.GetCustomAttribute(typeof(T), typeof(WriteToConsoleAttribute)) as WriteToConsoleAttribute;
        if (classAttribute is not null && !string.IsNullOrEmpty(classAttribute.Header))
        {
            Console.WriteLine(classAttribute.Header);
            indentation = "  ";
        }

        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            // Check if the property has the WriteToConsole attribute
            var propertyAttribute = Attribute.GetCustomAttribute(property, typeof(WriteToConsoleAttribute)) as WriteToConsoleAttribute;

            // Determine which mode to use (property attribute takes precedence over class attribute)
            WriteToConsoleMode mode = WriteToConsoleMode.Always;
            string? headerPrefix = indentation;

            if (propertyAttribute is not null)
            {
                mode = propertyAttribute.Mode;
                headerPrefix = propertyAttribute.Header ?? indentation;
            }
            else if (classAttribute is not null)
            {
                mode = classAttribute.Mode;
            }

            // If mode is Never, skip this property
            if (mode == WriteToConsoleMode.Never)
                continue;

            // Get the target type
            var targetType = property.PropertyType;
            var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableUnderlyingType ?? targetType;

            // Get the value
            var value = property.GetValue(instance);
            var stringValue = AsString(effectiveType, value);

            // Check if we should skip empty values
            if (mode == WriteToConsoleMode.IfNotEmpty && string.IsNullOrEmpty(stringValue))
                continue;

            // Write to the console based on the mode
            if (mode == WriteToConsoleMode.Masked)
            {
                Console.WriteLine($"{headerPrefix}{property.Name} = \"**MASKED**\"");
            }
            else
            {
                Console.WriteLine($"{headerPrefix}{property.Name} = \"{stringValue}\"");
            }
        }
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