using System;
using System.Linq;

namespace NetBricks;

public static class StringExtensions
{
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

    public static int AsInt(this string str, Func<int> dflt)
    {
        if (int.TryParse(str, out int val)) return val;
        return dflt();
    }

    public static bool AsBool(this string str, Func<bool> dflt)
    {
        if (new string[] { "true", "1", "yes" }.Contains(str?.ToLower())) return true;
        if (new string[] { "false", "0", "no" }.Contains(str?.ToLower())) return false;
        return dflt();
    }

    public static string[] AsArray(this string str, Func<string[]> dflt)
    {
        return string.IsNullOrEmpty(str)
            ? dflt()
            : str.Split(",").Select(id => id.Trim()).ToArray();
    }

    public static T AsEnum<T>(this string str, Func<T> dflt, Func<string, string> map = null) where T : struct
    {
        if (map != null) str = map(str);
        if (Enum.TryParse(str, true, out T val)) return val;
        return dflt();
    }
}