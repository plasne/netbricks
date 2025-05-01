using System;
using System.Linq;

namespace NetBricks;

public static class StringExtensions
{
    /// <summary>
    /// Return the string if it is not null or empty, otherwise return the default value.
    /// Useful for providing default values for strings.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static string? AsString(this string? str, Func<string?>? dflt)
    {
        return !string.IsNullOrEmpty(str)
            ? str
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as an integer, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static int? AsInt(this string? str, Func<int?>? dflt = null)
    {
        return int.TryParse(str, out int val)
            ? val
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as a double, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static double? AsDouble(this string? str, Func<double?>? dflt = null)
    {
        return double.TryParse(str, out double val)
            ? val
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as a float, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static float? AsFloat(this string? str, Func<float?>? dflt = null)
    {
        return float.TryParse(str, out float val)
            ? val
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as a long, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static long? AsLong(this string? str, Func<long?>? dflt = null)
    {
        return long.TryParse(str, out long val)
            ? val
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as a boolean, returning the default value if the string cannot be parsed.
    /// true, 1, and yes are considered true.
    /// false, 0, and no are considered false.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static bool? AsBool(this string? str, Func<bool?>? dflt = null)
    {
        if (new string[] { "true", "1", "yes" }.Contains(str?.ToLower())) return true;
        if (new string[] { "false", "0", "no" }.Contains(str?.ToLower())) return false;
        return dflt?.Invoke();
    }

    /// <summary>
    /// Split a string by commas and return the result as an array.
    /// If the string is null or empty, return the default delegate.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns>
    /// An array of strings.
    /// </returns>
    public static string[]? AsArray(this string? str, Func<string[]?>? dflt = null)
    {
        return !string.IsNullOrEmpty(str)
            ? str.Split(",").Select(id => id.Trim()).ToArray()
            : dflt?.Invoke();
    }

    /// <summary>
    /// Parse a string as an enum, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="str"></param>
    /// <param name="dflt">
    /// A delegate to return the default value if the string cannot be parsed.
    /// </param>
    /// <param name="map">
    /// A delegate to map the string to a different string before parsing.
    /// </param>
    /// <returns>
    /// A value of the enum type.
    /// </returns>
    public static T? AsEnum<T>(this string? str, Func<T?>? dflt = null, Func<string, string>? map = null) where T : struct, Enum
    {
        if (str is null) return dflt?.Invoke();
        if (map is not null) str = map(str);
        if (Enum.TryParse(str, true, out T val)) return val;
        return dflt?.Invoke();
    }
}