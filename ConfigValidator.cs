using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NetBricks;

internal class ConfigValidator<TOptions> : IValidateOptions<TOptions> where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);

        if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
        {
            foreach (var validationResult in validationResults)
            {
                Console.WriteLine(validationResult.ErrorMessage);
            }
            Environment.Exit(1);
        }

        return ValidateOptionsResult.Success;
    }
}

internal static class OptionsBuilderExtensions
{
    public static OptionsBuilder<TOptions> ValidateUsingConfigValidator<TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
        where TOptions : class
    {
        optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(new ConfigValidator<TOptions>());
        return optionsBuilder;
    }
}