using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class SetValueAttribute : ValidationAttribute
{
    private static readonly Dictionary<string, string> ErrorMessages = [];

    public string[] Keys { get; }

    public object? Default { get; set; }

    public SetValueAttribute(params string[] keys)
    {
        if (keys == null || keys.Length == 0)
        {
            throw new ArgumentException("At least one key must be provided", nameof(keys));
        }
        Keys = keys;
    }

    internal static void SetError(Type type, string? propertyName, string errorMessage)
    {
        string key = $"{type.FullName}.{propertyName}";
        ErrorMessages[key] = errorMessage;
    }

    internal static string? GetError(Type type, string? propertyName)
    {
        string key = $"{type.FullName}.{propertyName}";
        return ErrorMessages.TryGetValue(key, out var message) ? message : null;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        string? error = GetError(validationContext.ObjectType, validationContext.MemberName);
        if (!string.IsNullOrEmpty(error))
        {
            return new ValidationResult(error);
        }

        return ValidationResult.Success;
    }
}

internal static class SetValue
{
    internal static void Apply<T>(IConfiguration configuration, T instance) where T : class
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            // Check if the property has the GetValue attribute
            var attribute = Attribute.GetCustomAttribute(property, typeof(SetValueAttribute)) as SetValueAttribute;
            if (attribute is null)
                continue;

            // Try each key in order until we find a value
            string? value = null;
            foreach (var key in attribute.Keys)
            {
                value = configuration[key];
                if (!string.IsNullOrEmpty(value))
                    break;
            }

            if (!string.IsNullOrEmpty(value))
            {
                // Get the target type
                var targetType = property.PropertyType;
                var nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
                var effectiveType = nullableUnderlyingType ?? targetType;

                // Special handling for enums
                if (effectiveType == typeof(string))
                {
                    var convertedValue = value.AsString(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(bool))
                {
                    var convertedValue = value.AsBool(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(int))
                {
                    var convertedValue = value.AsInt(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(long))
                {
                    var convertedValue = value.AsLong(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(float))
                {
                    var convertedValue = value.AsFloat(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(double))
                {
                    var convertedValue = value.AsDouble(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType.IsEnum)
                {
                    if (Enum.TryParse(effectiveType, value, ignoreCase: true, out var convertedValue))
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(string[]) || effectiveType == typeof(IEnumerable<string>))
                {
                    var convertedValue = value.AsArray(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue);
                    }
                }
                else if (effectiveType == typeof(IList<string>) || effectiveType == typeof(List<string>))
                {
                    var convertedValue = value.AsArray(() => null);
                    if (convertedValue is not null)
                    {
                        property.SetValue(instance, convertedValue.ToList());
                    }
                }
                else
                {
                    SetValueAttribute.SetError(typeof(T), property.Name, $"Unsupported type {effectiveType.Name} for property {property.Name}.");
                }
            }
        }
    }
}