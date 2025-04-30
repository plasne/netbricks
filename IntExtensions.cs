using System;
using System.Linq;

namespace NetBricks;

public static class IntExtensions
{
    /// <summary>
    /// Clamp an integer value to a specified range.
    /// </summary>
    /// <param name="value">The integer value to clamp.</param>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <returns>The clamped value, which is guaranteed to be within the specified range.</returns>
    public static int Clamp(this int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}