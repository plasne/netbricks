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
    public static string AsString(this string str, Func<string> dflt = null)
    {
        if (string.IsNullOrEmpty(str))
        {
            if (dflt != null) return dflt();
            return null;
        }
        else
        {
            return str;
        }
    }

    /// <summary>
    /// Parse a string as an integer, returning the default value if the string cannot be parsed.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static int AsInt(this string str, Func<int> dflt)
    {
        if (int.TryParse(str, out int val)) return val;
        return dflt();
    }

    /// <summary>
    /// Parse a string as a boolean, returning the default value if the string cannot be parsed.
    /// true, 1, and yes are considered true.
    /// false, 0, and no are considered false.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dflt"></param>
    /// <returns></returns>
    public static bool AsBool(this string str, Func<bool> dflt)
    {
        if (new string[] { "true", "1", "yes" }.Contains(str?.ToLower())) return true;
        if (new string[] { "false", "0", "no" }.Contains(str?.ToLower())) return false;
        return dflt();
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
    public static string[] AsArray(this string str, Func<string[]> dflt)
    {
        return string.IsNullOrEmpty(str)
            ? dflt()
            : str.Split(",").Select(id => id.Trim()).ToArray();
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
    public static T AsEnum<T>(this string str, Func<T> dflt, Func<string, string> map = null) where T : struct
    {
        if (map != null) str = map(str);
        if (Enum.TryParse(str, true, out T val)) return val;
        return dflt();
    }
}