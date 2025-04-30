using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json; // System.Text.Json was not deserializing properly
using Microsoft.Extensions.Http;
using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Core;
using Microsoft.Identity.Client;
using System.Threading;

namespace NetBricks;

public class Config : IConfig, IDisposable
{
    private readonly IConfigProvider configProvider;
    private readonly DefaultAzureCredential defaultAzureCredential;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Lazy<HttpClient> httpClient;
    private readonly ConcurrentDictionary<string, object> cache = new();
    private bool disposed = false;

    public Config(
        IConfigProvider configProvider = null,
        DefaultAzureCredential defaultAzureCredential = null,
        IHttpClientFactory httpClientFactory = null)
    {
        this.configProvider = configProvider ?? new EnvVarChainConfigProvider();
        this.defaultAzureCredential = defaultAzureCredential;
        this.httpClientFactory = httpClientFactory;

        this.httpClient = new Lazy<HttpClient>(() =>
        {
            return this.httpClientFactory.CreateClient("netbricks");
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public DefaultAzureCredential DefaultAzureCredential => this.defaultAzureCredential;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
                if (this.httpClient.IsValueCreated)
                {
                    this.httpClient.Value.Dispose();
                }
            }

            // Dispose unmanaged resources.
            disposed = true;
        }
    }

    ~Config()
    {
        Dispose(false);
    }

    private class AppConfigItems
    {
        public AppConfigItem[] items = null;
    }

    private class AppConfigItem
    {
        public string content_type = null;
        public string key = null;
        public string value = null;
    }

    private class KeyVaultRef
    {
        public string uri = null;
    }

    public async Task<Dictionary<string, string>> LoadAsync(string[] filters, bool useFullyQualifiedName = false)
    {
        // exit if there is nothing requested
        Dictionary<string, string> kv = [];
        if (filters == null || filters.Length < 1) return kv;

        // check the requirements
        if (this.httpClientFactory is null || this.defaultAzureCredential is null)
        {
            throw new Exception("Config.Load: call AddHttpClientForConfig and AddDefaultAzureCredential before calling Load().");
        }

        // make sure the URL is provided
        if (string.IsNullOrEmpty(APPCONFIG_URL))
        {
            throw new Exception("Config.Load: set the APPCONFIG_URL environment variable to the URL of your Azure AppConfig instance.");
        }

        // get an access token
        var tokenRequestContext = new TokenRequestContext([$"{APPCONFIG_URL}/.default"]);
        var tokenResponse = await this.defaultAzureCredential.GetTokenAsync(tokenRequestContext);
        var accessToken = tokenResponse.Token;

        // process each key filter request
        foreach (var filter in filters)
        {
            // make authenticated calls to Azure AppConfig
            using var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{APPCONFIG_URL}/kv?key={filter}"),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            using (var response = await this.httpClient.Value.SendAsync(request))
            {
                // evaluate the response
                var raw = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    throw new Exception($"Config.Load: The identity is not authorized to get key/value pairs from the AppConfig \"{APPCONFIG_URL}\"; make sure this is the right instance and that you have granted rights to the Managed Identity or Service Principal. If running locally, make sure you have run an \"az login\" with the correct account and subscription.");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Config.Load: HTTP {(int)response.StatusCode} - {raw}");
                }

                // look for key/value pairs
                var json = JsonConvert.DeserializeObject<AppConfigItems>(raw);
                foreach (var item in json.items)
                {
                    var key = useFullyQualifiedName ? item.key : item.key.Split(":").Last();
                    key = key.ToUpper();
                    var val = item.value;
                    if (item.content_type != null && item.content_type.Contains("vnd.microsoft.appconfig.keyvaultref", StringComparison.InvariantCultureIgnoreCase))
                    {
                        val = JsonConvert.DeserializeObject<KeyVaultRef>(item.value).uri;
                    }
                    kv.TryAdd(key, val);
                }
            }
            ;
        }

        return kv;
    }

    public async Task ApplyAsync(string[] filters = null)
    {
        // show configuration
        if (!string.IsNullOrEmpty(APPCONFIG_URL))
            Console.WriteLine($"APPCONFIG_URL = \"{APPCONFIG_URL}\"");

        if (CONFIG_KEYS is not null && CONFIG_KEYS.Length > 0)
            Console.WriteLine($"CONFIG_KEYS = \"{string.Join(", ", CONFIG_KEYS)}\"");

        // load the config
        filters ??= CONFIG_KEYS;
        Dictionary<string, string> kv = await LoadAsync(filters);

        // apply the config
        foreach (var pair in kv)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }

        // log
        Console.WriteLine($"LOADED KEYS: {string.Join(", ", kv.Keys)}");
    }

    private class KeyVaultItem
    {
        public string value = null;
    }

    public async Task<string> GetFromKeyVaultAsync(string posurl, bool ignore404 = false)
    {
        // shortcut if the URL is empty or not a key vault URL
        if (string.IsNullOrEmpty(posurl) ||
            !posurl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) ||
            !posurl.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase))
        {
            return posurl;
        }

        // check the requirements
        if (this.httpClientFactory is null || this.defaultAzureCredential is null)
        {
            throw new Exception("Config.GetFromKeyVault: call AddHttpClientForConfig and AddDefaultAzureCredential before calling Load().");
        }

        // get an access token
        var tokenRequestContext = new TokenRequestContext([$"https://vault.azure.net/.default"]);
        var tokenResponse = await this.defaultAzureCredential.GetTokenAsync(tokenRequestContext);
        var accessToken = tokenResponse.Token;

        // get from the keyvault
        using (var request = new HttpRequestMessage()
        {
            RequestUri = new Uri($"{posurl}?api-version=7.0"),
            Method = HttpMethod.Get
        })
        {
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            using var response = await this.httpClient.Value.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            if (ignore404 && (int)response.StatusCode == 404) // Not Found
            {
                return string.Empty;
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Config.GetFromKeyVault: HTTP {(int)response.StatusCode} - {raw}");
            }
            var item = JsonConvert.DeserializeObject<KeyVaultItem>(raw);
            return item.value;
        }
    }

    public static string GetOnce(params string[] keys)
    {
        foreach (string key in keys)
        {
            string val = System.Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(val)) return val;
        }
        return null;
    }

    public async Task<T> GetSecretAsync<T>(string key, Func<string, T> convert = null, bool ignore404 = false)
    {
        // ensure this is string or has a convert method
        if (typeof(T) != typeof(string) && convert == null)
        {
            throw new Exception("Config.GetSecret<T>: cannot be used without convert except when the datatype is string.");
        }

        // get from cache
        if (TryGetFromCache<T>(key, out T val)) return val;

        // get from environment variable
        var str = this.configProvider.Get(key);

        // get from key vault
        str = await GetFromKeyVaultAsync(str, ignore404);

        // IMPORTANT: all Get methods should ensure empty strings are returned as null to support ??
        // EXCEPTION: it is possible that the convert() might do something different
        if (string.IsNullOrEmpty(str)) str = null;

        // convert
        if (convert != null)
        {
            val = convert(str);
        }
        else
        {
            val = (T)(object)str;
        }

        // add to cache
        AddToCache<T>(key, val);

        return val;
    }

    public async Task<string> GetSecretAsync(string key, bool ignore404 = false)
    {
        return await GetSecretAsync<string>(key, null, ignore404);
    }

    public T Get<T>(string key, Func<string, T> convert = null)
    {
        // ensure this is string or has a convert method
        if (typeof(T) != typeof(string) && convert == null)
        {
            throw new ArgumentNullException("Config.Get: cannot be used without convert except when the datatype is string.");
        }

        // get from cache
        if (TryGetFromCache<T>(key, out T val)) return val;

        // get from environment variable
        var str = this.configProvider.Get(key);

        // IMPORTANT: all Get methods should ensure empty strings are returned as null to support ??
        // EXCEPTION: it is possible that the convert() might do something different
        if (string.IsNullOrEmpty(str)) str = null;

        // convert
        if (convert != null)
        {
            val = convert(str);
        }
        else
        {
            val = (T)(object)str;
        }

        // add to cache
        AddToCache<T>(key, val);

        return val;
    }

    public string Get(string key)
    {
        return Get<string>(key, null);
    }

    public bool TryGetFromCache<T>(string key, out T val)
    {
        if (this.cache.TryGetValue(key, out object obj))
        {
            val = (T)obj;
            return true;
        }
        else
        {
            val = default;
            return false;
        }
    }

    public void AddToCache<T>(string key, T val)
    {
        this.cache.TryAdd(key, val);
    }

    public void RemoveFromCache(string key)
    {
        this.cache.TryRemove(key, out object _);
    }

    private static string HideIfAppropriate(string value, bool hideValue)
    {
        if (!hideValue) return value;
        if (value.StartsWith("https://") && value.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase)) return value;
        return "(set)";
    }

    private static void PrintException(string message)
    {
        Console.WriteLine(message);
        throw new Exception(message);
    }

    /// <summary>
    /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="hideValue">If true, the value is redacted (for instance, use this for passwords).</param>
    public static void Require(string key, string value, bool hideValue = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            PrintException($"{key} is REQUIRED but missing.");
        }
        else
        {
            string val = HideIfAppropriate(value, hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
        }
    }

    /// <summary>
    /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="hideValue">If true, the value is redacted (for instance, use this for passwords).</param>
    public static void Require(string key, string[] values, bool hideValue = false)
    {
        if (!values.Any(v => v.Trim().Length > 0))
        {
            PrintException($"{key} is REQUIRED but missing.");
        }
        else
        {
            string val = HideIfAppropriate(string.Join(", ", values), hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
        }
    }

    /// <summary>
    /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public static void Require(string key, int value)
    {
        if (value == 0)
        {
            PrintException($"{key} is REQUIRED but is set to 0.");
        }
        else
        {
            Console.WriteLine($"{key} = \"{value}\"");
        }
    }

    /// <summary>
    /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    public static void Require(string key, int value, int min = int.MinValue, int max = int.MaxValue)
    {
        if (value < min)
        {
            PrintException($"{key} is REQUIRED but is less than the minimum of {min}.");
        }
        else if (value > max)
        {
            PrintException($"{key} is REQUIRED but is greater than the maximum of {max}.");
        }
        else
        {
            Console.WriteLine($"{key} = \"{value}\"");
        }
    }

    /// <summary>
    /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The enum type of the value</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="value">The enum value</param>
    public static void Require<T>(string key, T value) where T : struct, Enum
    {
        if (Convert.ToInt32(value) == 0)
        {
            PrintException($"{key} is REQUIRED but is set to {value}.");
        }
        else
        {
            Console.WriteLine($"{key} = \"{value}\"");
        }
    }

    /// <summary>
    /// Makes a configuration value optional.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="hideValue">If true, the value is redacted (for instance, use this for passwords).</param>
    /// <param name="hideIfEmpty">If true and the value is empty, the print line will be suppressed.</param>
    /// <returns>
    /// A boolean indicating whether the value is set.
    /// </returns>
    public static bool Optional(string key, string value, bool hideValue = false, bool hideIfEmpty = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (!hideIfEmpty) Console.WriteLine($"{key} is \"(not-set)\".");
            return false;
        }
        else
        {
            string val = HideIfAppropriate(value, hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
            return true;
        }
    }

    /// <summary>
    /// Makes a configuration value optional.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="hideValue">If true, the value is redacted (for instance, use this for passwords).</param>
    /// <param name="hideIfEmpty">If true and the value is empty, the print line will be suppressed.</param>
    /// <returns>
    /// A boolean indicating whether the value is set.
    /// </returns>
    public static bool Optional(string key, string[] values, bool hideValue = false, bool hideIfEmpty = false)
    {
        if (values == null || values.Count(v => !string.IsNullOrWhiteSpace(v)) < 1)
        {
            if (!hideIfEmpty) Console.WriteLine($"{key} is \"(not-set)\".");
            return false;
        }
        else
        {
            string val = HideIfAppropriate(string.Join(", ", values), hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
            return true;
        }
    }

    /// <summary>
    /// Makes a configuration value optional.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>
    /// A boolean indicating whether the value is set.
    /// </returns>
    public static bool Optional(string key, bool value)
    {
        Console.WriteLine($"{key} = \"{value}\"");
        return true;
    }

    /// <summary>
    /// Makes a configuration value optional.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>
    /// A boolean indicating whether the value is set.
    /// </returns>
    public static bool Optional(string key, int value)
    {
        Console.WriteLine($"{key} = \"{value}\"");
        return value != 0;
    }

    /// <summary>
    /// Makes a configuration value optional.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>
    /// A boolean indicating whether the value is set.
    /// </returns>
    public static bool Optional<T>(string key, T value) where T : struct, Enum
    {
        Console.WriteLine($"{key} = \"{value}\"");
        return Convert.ToInt32(value) != 0;
    }

    /// <summary>
    /// Get the value of the LOG_LEVEL environment variable.
    /// </summary>
    /// <returns>The value of the LOG_LEVEL environment variable.</returns>
    public static Microsoft.Extensions.Logging.LogLevel LOG_LEVEL
    {
        get => GetOnce("LOG_LEVEL").AsEnum<Microsoft.Extensions.Logging.LogLevel>(() => Microsoft.Extensions.Logging.LogLevel.Information);
    }

    /// <summary>
    /// Get the value of the DISABLE_COLORS environment variable.
    /// </summary>
    /// <returns>The value of the DISABLE_COLORS environment variable.</returns>
    public static bool DISABLE_COLORS
    {
        get => GetOnce("DISABLE_COLORS").AsBool(() => false);
    }

    /// <summary>
    /// Get the value of the APPCONFIG or APPCONFIG_URL environment variable.
    /// </summary>
    /// <returns>The value of the APPCONFIG or APPCONFIG_URL environment variable.</returns>
    public static string APPCONFIG_URL
    {
        get
        {
            var val = GetOnce("APPCONFIG", "APPCONFIG_URL");
            if (!string.IsNullOrEmpty(val))
            {
                val = val.ToLower();
                if (!val.Contains(".azconfig.io")) val += ".azconfig.io";
                if (!val.StartsWith("https://")) val = "https://" + val;
            }
            return val;
        }
    }

    /// <summary>
    /// Get the value of the CONFIG_KEYS environment variable.
    /// </summary>
    /// <returns>The value of the CONFIG_KEYS environment variable.</returns>
    public static string[] CONFIG_KEYS
    {
        get => GetOnce("CONFIG_KEYS").AsArray(() => null);
    }

    /// <summary>
    /// Get the value of the ASPNETCORE_ENVIRONMENT environment variable.
    /// </summary>
    /// <returns>The value of the ASPNETCORE_ENVIRONMENT environment variable.</returns>
    public static string ASPNETCORE_ENVIRONMENT
    {
        get => GetOnce("ASPNETCORE_ENVIRONMENT").AsString(() => "Development");
    }

    /// <summary>
    /// Get the value of the INCLUDE_CREDENTIAL_TYPES environment variable.
    /// </summary>
    /// <returns>The value of the INCLUDE_CREDENTIAL_TYPES environment variable.</returns>
    public static string[] INCLUDE_CREDENTIAL_TYPES
    {
        get => GetOnce("INCLUDE_CREDENTIAL_TYPES").AsArray(() => []);
    }
}