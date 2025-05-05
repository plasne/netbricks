using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace NetBricks;

/// <summary>
/// Attribute that marks a method to be called during the configuration setup process.
/// This attribute enables defining custom setup logic via methods that will be
/// automatically executed as part of the configuration initialization.
/// </summary>
/// <remarks>
/// Methods marked with this attribute:
/// - Must be instance methods of the configuration class
/// - Can be void or return Task (for async operations)
/// - Will be called in the order specified by the Order property
/// - Should not have parameters
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SetValuesAttribute : Attribute
{
    /// <summary>
    /// Gets the execution order of the method. Methods with lower order values are executed first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SetValuesAttribute"/> class with the specified execution order.
    /// </summary>
    /// <param name="order">The execution order. Default is 0.</param>
    public SetValuesAttribute(int order = 0)
    {
        Order = order;
    }
}

internal static class SetValues
{
    internal static async Task ApplyAsync<T>(T instance, CancellationToken cancellationToken = default)
        where T : class
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
                await Task.WhenAny(
                    task,
                    Task.Delay(Timeout.Infinite, cancellationToken)
                ).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}