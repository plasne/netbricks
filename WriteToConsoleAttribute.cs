using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

// TODO: Add a skip if empty
// TODO: Add a WriteToLogAttribute

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class WriteToConsoleAttribute : Attribute
{
    public string? Header { get; set; }
    public bool Mask { get; set; }

    public WriteToConsoleAttribute(string? header = null, bool mask = false)
    {
        this.Header = header;
        this.Mask = mask;
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
            // Check if the property has the GetValue attribute
            var propertyAttribute = Attribute.GetCustomAttribute(property, typeof(WriteToConsoleAttribute)) as WriteToConsoleAttribute;

            // Get the target type
            var targetType = property.PropertyType;
            var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
            var effectiveType = nullableUnderlyingType ?? targetType;

            var value = AsString(effectiveType, property.GetValue(instance));

            // Write to the console
            if (propertyAttribute is not null && propertyAttribute.Mask)
            {
                Console.WriteLine($"{propertyAttribute.Header ?? indentation}{property.Name} = \"**MASKED**\"");
            }
            else if (propertyAttribute is not null)
            {
                Console.WriteLine($"{propertyAttribute.Header ?? indentation}{property.Name} = \"{value}\"");
            }
            else if (classAttribute is not null && classAttribute.Mask)
            {
                Console.WriteLine($"{indentation}{property.Name} = \"**MASKED**\"");
            }
            else if (classAttribute is not null)
            {
                Console.WriteLine($"{indentation}{property.Name} = \"{value}\"");
            }
        }
    }

    internal static string? AsString(Type? type, object? value)
    {
        if (type == typeof(string[]))
            return string.Join(", ", value as string[] ?? []);
        if (type == typeof(IEnumerable<string>))
            return string.Join(", ", value as IEnumerable<string> ?? []);
        if (type == typeof(IList<string>))
            return string.Join(", ", value as IList<string> ?? []);
        if (type == typeof(List<string>))
            return string.Join(", ", value as List<string> ?? []);
        return value?.ToString();
    }
}