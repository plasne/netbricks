using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json; // System.Text.Json was not deserializing properly
using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Core;

namespace NetBricks;

public class Config : IConfig, IDisposable
{
    private readonly DefaultAzureCredential defaultAzureCredential;
    private HttpClient httpClient;
    private readonly ConcurrentDictionary<string, object> cache = new();
    private readonly IConfigProvider configProvider;
    private bool disposed = false;

    public Config(IConfigProvider configProvider = null)
    {
        this.configProvider = configProvider ?? new EnvVarChainConfigProvider();

        // get the list of credential options
        string[] include = (INCLUDE_CREDENTIAL_TYPES.Length > 0)
            ? INCLUDE_CREDENTIAL_TYPES :
            string.Equals(ASPNETCORE_ENVIRONMENT, "Development", StringComparison.InvariantCultureIgnoreCase)
                ? ["azcli", "env"]
                : ["env", "mi"];

        // log
        Console.WriteLine($"INCLUDE_CREDENTIAL_TYPES = \"{string.Join(", ", include)}\"");

        // build the credential object
        this.defaultAzureCredential = new DefaultAzureCredential(
            new DefaultAzureCredentialOptions()
            {
                ExcludeEnvironmentCredential = !include.Contains("env"),
                ExcludeManagedIdentityCredential = !include.Contains("mi"),
                ExcludeSharedTokenCacheCredential = !include.Contains("token"),
                ExcludeVisualStudioCredential = !include.Contains("vs"),
                ExcludeVisualStudioCodeCredential = !include.Contains("vscode"),
                ExcludeAzureCliCredential = !include.Contains("azcli"),
                ExcludeInteractiveBrowserCredential = !include.Contains("browser"),
            });
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
                this.httpClient?.Dispose();
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

    public async Task<Dictionary<string, string>> Load(string[] filters, bool useFullyQualifiedName = false)
    {
        // exit if there is nothing requested or no way to get it
        Dictionary<string, string> kv = [];
        if (string.IsNullOrEmpty(APPCONFIG_URL)) return kv;
        if (filters == null || filters.Length < 1) return kv;

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
            this.httpClient ??= new HttpClient();
            using (var response = await this.httpClient.SendAsync(request))
            {
                // evaluate the response
                var raw = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    throw new Exception($"Load: The identity is not authorized to get key/value pairs from the AppConfig \"{APPCONFIG_URL}\"; make sure this is the right instance and that you have granted rights to the Managed Identity or Service Principal. If running locally, make sure you have run an \"az login\" with the correct account and subscription.");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Load: HTTP {(int)response.StatusCode} - {raw}");
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
            };
        }

        return kv;
    }

    public async Task Apply(string[] filters = null)
    {
        // show configuration
        if (!string.IsNullOrEmpty(APPCONFIG_URL))
            Console.WriteLine($"APPCONFIG_URL = \"{APPCONFIG_URL}\"");

        if (CONFIG_KEYS is not null && CONFIG_KEYS.Length > 0)
            Console.WriteLine($"CONFIG_KEYS = \"{string.Join(", ", CONFIG_KEYS)}\"");

        // load the config
        filters ??= CONFIG_KEYS;
        Dictionary<string, string> kv = await Load(filters);

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

    public async Task<string> GetFromKeyVault(string posurl, bool ignore404 = false)
    {
        if (!string.IsNullOrEmpty(posurl) &&
            posurl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) &&
            posurl.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase))
        {
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
                this.httpClient ??= new HttpClient();
                using var response = await this.httpClient.SendAsync(request);
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
            };
        }
        else
        {
            return posurl;
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

    /// <summary>
    /// The GetSecret() method supports pulling from cache, getting the value from the environment
    /// variables, resolving in key vault, converting to the specified datatype, and storing
    /// in a variable.
    /// </summary>
    public async Task<T> GetSecret<T>(string key, Func<string, T> convert = null, bool ignore404 = false)
    {
        // ensure this is string or has a convert method
        if (typeof(T) != typeof(string) && convert == null)
        {
            throw new Exception("Get<T>() cannot be used without convert except when the datatype is string.");
        }

        // get from cache
        if (GetFromCache<T>(key, out T val)) return val;

        // get from environment variable
        var str = this.configProvider.Get(key);

        // get from key vault
        str = await GetFromKeyVault(str, ignore404);

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

    /// <summary>
    /// This Get() method supports pulling from cache, getting the value from the environment
    /// variables, converting to the specified datatype, and storing in a variable.
    /// </summary>
    public T Get<T>(string key, Func<string, T> convert = null)
    {
        // ensure this is string or has a convert method
        if (typeof(T) != typeof(string) && convert == null)
        {
            throw new ArgumentNullException("Get<T>() cannot be used without convert except when the datatype is string.");
        }

        // get from cache
        if (GetFromCache<T>(key, out T val)) return val;

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

    public bool GetFromCache<T>(string key, out T val)
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

    private string HideIfAppropriate(string value, bool hideValue)
    {
        if (!hideValue) return value;
        if (value.StartsWith("https://") && value.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase)) return value;
        return "(set)";
    }

    public void Require(string key, string value, bool hideValue = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"{key} is REQUIRED but missing.");
            throw new Exception($"{key} is REQUIRED but missing.");
        }
        else
        {
            string val = HideIfAppropriate(value, hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
        }
    }

    public void Require(string key, string[] values, bool hideValue = false)
    {
        if (!values.Any(v => v.Trim().Length > 0))
        {
            Console.WriteLine($"{key} is REQUIRED but missing.");
            throw new Exception($"{key} is REQUIRED but missing.");
        }
        else
        {
            string val = HideIfAppropriate(string.Join(", ", values), hideValue);
            Console.WriteLine($"{key} = \"{val}\"");
        }
    }

    public void Require(string key, bool value, bool hideValue = false)
    {
        Require(key, value.ToString(), hideValue);
    }

    public void Require(string key, int value, bool hideValue = false)
    {
        Require(key, value.ToString(), hideValue);
    }

    public void Require(string key, bool hideValue = false)
    {
        string value = this.configProvider.Get(key);
        Require(key, value, hideValue);
    }

    public bool Optional(string key, string value, bool hideValue = false, bool hideIfEmpty = false)
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

    public bool Optional(string key, string[] values, bool hideValue = false, bool hideIfEmpty = false)
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

    public bool Optional(string key, bool value, bool hideValue = false, bool hideIfEmpty = false)
    {
        string val = HideIfAppropriate(value.ToString(), hideValue);
        Console.WriteLine($"{key} = \"{val}\"");
        return true;
    }

    public bool Optional(string key, int value, bool hideValue = false, bool hideIfEmpty = false)
    {
        string val = HideIfAppropriate(value.ToString(), hideValue);
        Console.WriteLine($"{key} = \"{val}\"");
        return true;
    }

    public bool Optional(string key, bool hideValue = false, bool hideIfEmpty = false)
    {
        string value = this.configProvider.Get(key);
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

    public static LogLevel LOG_LEVEL
    {
        get => GetOnce("LOG_LEVEL").AsEnum<LogLevel>(() => LogLevel.Information);
    }

    public static bool DISABLE_COLORS
    {
        get => GetOnce("DISABLE_COLORS").AsBool(() => false);
    }

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

    public static string[] CONFIG_KEYS
    {
        get => GetOnce("CONFIG_KEYS").AsArray(() => null);
    }

    public static string ASPNETCORE_ENVIRONMENT
    {
        get => GetOnce("ASPNETCORE_ENVIRONMENT").AsString(() => "Development");
    }

    public static string[] INCLUDE_CREDENTIAL_TYPES
    {
        get => GetOnce("INCLUDE_CREDENTIAL_TYPES").AsArray(() => []);
    }
}