using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

// TODO: consider supporting multiple calls to Key Vault at the same time

namespace NetBricks;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ResolveSecretAttribute : ValidationAttribute
{
    internal string? ErrorMessageRaisedDuringApply { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string posurl)
        {
            return new ValidationResult("ResolveSecret can only be applied to Strings.");
        }

        if (!string.IsNullOrEmpty(this.ErrorMessageRaisedDuringApply))
        {
            return new ValidationResult(this.ErrorMessageRaisedDuringApply);
        }

        if (posurl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) &&
            posurl.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase))
        {
            return new ValidationResult("A secret was not found in the key vault.");
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
        IConfiguration configuration,
        T instance,
        IHttpClientFactory? httpClientFactory = null,
        DefaultAzureCredential? defaultAzureCredential = null)
        where T : class
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            // Check if the property has the GetValue attribute
            var attribute = Attribute.GetCustomAttribute(property, typeof(ResolveSecretAttribute)) as ResolveSecretAttribute;
            if (attribute is null)
                continue;

            // only process strings
            var value = property.GetValue(instance);
            if (value is not string posurl)
            {
                continue;
            }

            // shortcut if the URL is empty or not a key vault URL
            if (string.IsNullOrEmpty(posurl) ||
                !posurl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) ||
                !posurl.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            // check the requirements
            if (httpClientFactory is null || defaultAzureCredential is null)
            {
                attribute.ErrorMessageRaisedDuringApply = "HttpClientFactory and DefaultAzureCredential must be provided.";
            }

            // get an access token
            var tokenRequestContext = new TokenRequestContext([$"https://vault.azure.net/.default"]);
            var tokenResponse = await defaultAzureCredential!.GetTokenAsync(tokenRequestContext);
            var accessToken = tokenResponse.Token;

            // create the HTTP client
            using var httpClient = httpClientFactory!.CreateClient("netbricks");

            // get from the keyvault
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
                    attribute.ErrorMessageRaisedDuringApply = $"Key vault request failed: {response.StatusCode} - {raw}";
                    continue;
                }
                var item = JsonConvert.DeserializeObject<KeyVaultItem>(raw);
                property.SetValue(instance, item?.value);
            }
        }
    }
}
