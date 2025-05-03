using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

// TODO: consider supporting multiple calls to Key Vault at the same time
// TODO: consider supporting types other than string

namespace NetBricks;

/// <summary>
/// Attribute that marks a string property for resolving a Key Vault secret reference.
/// This attribute is applied to string properties that contain Azure Key Vault URLs.
/// During configuration setup, these URLs will be replaced with the actual secret values.
/// </summary>
/// <remarks>
/// Currently only supports string properties containing direct Key Vault URLs.
/// Future enhancements may include supporting other property types and batch operations.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ResolveSecretAttribute : ValidationAttribute
{
    private static readonly Dictionary<string, string> ErrorMessages = [];

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
        if (value is not string posurl)
        {
            return new ValidationResult("ResolveSecret can only be applied to Strings.");
        }

        string? error = GetError(validationContext.ObjectType, validationContext.MemberName);
        if (!string.IsNullOrEmpty(error))
        {
            return new ValidationResult(error);
        }

        return ValidationResult.Success;
    }
}

internal static class ResolveSecret
{
    private class KeyVaultItem
    {
        public string? value = null;
    }

    internal static async Task ApplyAsync<T>(
        T instance,
        IHttpClientFactory? httpClientFactory = null,
        DefaultAzureCredential? defaultAzureCredential = null)
        where T : class
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        // look for properties with the ResolveSecret attribute
        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            // check if the property has the ResolveSecret attribute
            var attribute = Attribute.GetCustomAttribute(property, typeof(ResolveSecretAttribute)) as ResolveSecretAttribute;
            if (attribute is null)
                continue;

            // only process strings
            var value = property.GetValue(instance);
            if (value is not string posurl)
                continue;

            // shortcut if the URL is empty or not a key vault URL
            if (string.IsNullOrEmpty(posurl) ||
                !posurl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                !posurl.Contains(".vault.azure.net/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // check the requirements
            if (httpClientFactory is null || defaultAzureCredential is null)
            {
                ResolveSecretAttribute.SetError(typeof(T), property.Name, "HttpClientFactory and DefaultAzureCredential must be provided.");
                continue;
            }

            // get an access token
            var tokenRequestContext = new TokenRequestContext([$"https://vault.azure.net/.default"]);
            var tokenResponse = await defaultAzureCredential!.GetTokenAsync(tokenRequestContext);
            var accessToken = tokenResponse.Token;

            // create the HTTP client
            using var httpClient = httpClientFactory!.CreateClient("netbricks");

            // get from the Key Vault
            using (var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{posurl}?api-version=7.0"),
                Method = HttpMethod.Get
            })
            {
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                using var response = await httpClient.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    ResolveSecretAttribute.SetError(typeof(T), property.Name, $"Key vault request failed: {response.StatusCode} - {raw}");
                    continue;
                }
                var item = JsonConvert.DeserializeObject<KeyVaultItem>(raw);
                property.SetValue(instance, item?.value);
            }
        }
    }
}
