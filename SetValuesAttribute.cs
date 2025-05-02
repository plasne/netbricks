using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SetValuesAttribute : Attribute
{
    public int Order { get; }

    public SetValuesAttribute(int order = 0)
    {
        Order = order;
    }
}

internal static class SetValues
{
    internal static async Task ApplyAsync<T>(T instance) where T : class
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var methods = typeof(T).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(SetValuesAttribute), false).Any())
            .OrderBy(m => ((SetValuesAttribute)m.GetCustomAttributes(typeof(SetValuesAttribute), false).First()).Order);
        foreach (var method in methods)
        {
            var task = method.Invoke(instance, null) as Task;
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
    }
}