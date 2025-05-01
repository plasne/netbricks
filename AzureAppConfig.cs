using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace NetBricks;

internal class AzureAppConfig
{
    public AzureAppConfig(IOptions<ConfigOptions> options, IHttpClientFactory? httpClientFactory = null, DefaultAzureCredential? defaultAzureCredential = null)
    {
        this.options = options.Value;
        this.httpClientFactory = httpClientFactory;
        this.defaultAzureCredential = defaultAzureCredential;
    }

    private readonly ConfigOptions options;
    private readonly IHttpClientFactory? httpClientFactory;
    private readonly DefaultAzureCredential? defaultAzureCredential;

    private class AppConfigItems
    {
        public AppConfigItem[]? items = null;
    }

    private class AppConfigItem
    {
        public string? content_type = null;
        public required string key;
        public required string value;
    }

    private class KeyVaultRef
    {
        public string? uri = null;
    }

    internal async Task LoadAsync()
    {
        // exit if there is nothing requested
        if (this.options.APPCONFIG_KEYS is null || this.options.APPCONFIG_KEYS.Length < 1) return;

        // check the requirements
        if (this.httpClientFactory is null || this.defaultAzureCredential is null)
        {
            throw new Exception("Config.Load: call AddHttpClientForConfig and AddDefaultAzureCredential before calling Load().");
        }

        // make sure the URL is provided
        if (string.IsNullOrEmpty(this.options.APPCONFIG_URL))
        {
            throw new Exception("Config.Load: set the APPCONFIG_URL environment variable to the URL of your Azure AppConfig instance.");
        }

        // get an access token
        var tokenRequestContext = new TokenRequestContext([$"{this.options.APPCONFIG_URL}/.default"]);
        var tokenResponse = await defaultAzureCredential.GetTokenAsync(tokenRequestContext);
        var accessToken = tokenResponse.Token;

        // create the HTTP client
        using var httpClient = httpClientFactory.CreateClient("netbricks");

        // process each key filter request
        foreach (var filter in this.options.APPCONFIG_KEYS)
        {
            // make authenticated calls to Azure AppConfig
            using var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{this.options.APPCONFIG_URL}/kv?key={filter}"),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            using (var response = await httpClient.SendAsync(request))
            {
                // evaluate the response
                var raw = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    throw new Exception($"Config.Load: The identity is not authorized to get key/value pairs from the AppConfig \"{this.options.APPCONFIG_URL}\"; make sure this is the right instance and that you have granted rights to the Managed Identity or Service Principal. If running locally, make sure you have run an \"az login\" with the correct account and subscription.");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Config.Load: HTTP {(int)response.StatusCode} - {raw}");
                }

                // look for key/value pairs
                var json = JsonConvert.DeserializeObject<AppConfigItems>(raw);
                if (json is not null && json.items is not null)
                {
                    foreach (var item in json.items)
                    {
                        var key = this.options.APPCONFIG_SHOULD_USE_FULLY_QUALIFIED_KEYS
                            ? item.key
                            : item.key.Split(":").Last();
                        key = key.ToUpper();
                        var val = item.value;
                        if (item.content_type != null && item.content_type.Contains("vnd.microsoft.appconfig.keyvaultref", StringComparison.InvariantCultureIgnoreCase))
                        {
                            val = JsonConvert.DeserializeObject<KeyVaultRef>(item.value)?.uri;
                        }
                        if (!string.IsNullOrEmpty(val) && Environment.GetEnvironmentVariable(key) is null)
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
            }
        }
    }
}