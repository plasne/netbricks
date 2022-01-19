using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json; // System.Text.Json was not deserializing properly
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;

namespace NetBricks
{

    public static class AddConfigConfiguration
    {

        public static void AddConfig<T>(this IServiceCollection services) where T : class, IConfig
        {
            services.TryAddSingleton<IConfig, T>();
        }

        public static void AddConfig(this IServiceCollection services)
        {
            services.TryAddSingleton<IConfig, Config>();
        }

    }

    public class Config : IConfig
    {

        public Config(
            ILogger<Config> logger,
            IAccessTokenFetcher accessTokenFetcher,
            IHttpClientFactory httpClientFactory,
            IConfigProvider configProvider = null
        )
        {
            this.Logger = logger;
            this.AccessTokenFetcher = accessTokenFetcher;
            this.HttpClient = httpClientFactory.CreateClient("netbricks");
            this.Cache = new ConcurrentDictionary<string, object>();
            this.ConfigProvider = configProvider ?? new EnvVarConfigProvider();
        }

        private ILogger<Config> Logger { get; }
        private IAccessTokenFetcher AccessTokenFetcher { get; }
        private HttpClient HttpClient { get; }
        private IConfigProvider ConfigProvider { get; }
        private ConcurrentDictionary<string, object> Cache { get; }

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
            Dictionary<string, string> kv = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(AppConfig)) return kv;
            if (filters == null || filters.Length < 1) return kv;

            // get an accessToken
            string accessToken = await AccessTokenFetcher.GetAccessToken($"https://{AppConfig}", "CONFIG");

            // process each key filter request
            foreach (var filter in filters)
            {

                // make authenticated calls to Azure AppConfig
                using (var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"https://{AppConfig}/kv?key={filter}"),
                    Method = HttpMethod.Get
                })
                {
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    using (var response = await this.HttpClient.SendAsync(request))
                    {

                        // evaluate the response
                        var raw = await response.Content.ReadAsStringAsync();
                        if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                        {
                            throw new Exception($"Load: The identity is not authorized to get key/value pairs from the AppConfig \"{AppConfig}\"; make sure this is the right instance and that you have granted rights to the Managed Identity or Service Principal. If running locally, make sure you have run an \"az login\" with the correct account and subscription.");
                        }
                        else if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception($"Load: HTTP {(int)response.StatusCode} - {raw}");
                        }

                        // look for key/value pairs
                        var json = JsonConvert.DeserializeObject<AppConfigItems>(raw);
                        foreach (var item in json.items)
                        {
                            Logger.LogDebug($"Config.Load: loaded \"{item.key}\" = \"{item.value}\".");
                            var key = (useFullyQualifiedName) ? item.key : item.key.Split(":").Last();
                            key = key.ToUpper();
                            var val = item.value;
                            if (item.content_type != null && item.content_type.Contains("vnd.microsoft.appconfig.keyvaultref", StringComparison.InvariantCultureIgnoreCase))
                            {
                                val = JsonConvert.DeserializeObject<KeyVaultRef>(item.value).uri;
                            }
                            if (!kv.ContainsKey(key)) kv.Add(key, val);
                        }

                    };

                }
            }

            return kv;
        }

        public async Task Apply(string[] filters = null)
        {

            // load the config
            if (filters == null) filters = ConfigKeys;
            Dictionary<string, string> kv = await Load(filters);

            // apply the config
            foreach (var pair in kv)
            {
                System.Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

        }

        private class KeyVaultItem
        {
            public string value = null;
        }

        public async Task<string> GetFromKeyVault(string posurl, bool ignore404 = false)
        {
            if (
                !string.IsNullOrEmpty(posurl) &&
                posurl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) &&
                posurl.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase)
            )
            {

                // get an access token
                var accessToken = await AccessTokenFetcher.GetAccessToken("https://vault.azure.net", "VAULT");

                // get from the keyvault
                using (var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"{posurl}?api-version=7.0"),
                    Method = HttpMethod.Get
                })
                {
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                    using (var response = await this.HttpClient.SendAsync(request))
                    {
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
            var str = this.ConfigProvider.Get(key);

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
            var str = this.ConfigProvider.Get(key);

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
            if (this.Cache.TryGetValue(key, out object obj))
            {
                val = (T)obj;
                return true;
            }
            else
            {
                val = default(T);
                return false;
            }
        }

        public void AddToCache<T>(string key, T val)
        {
            this.Cache.TryAdd(key, val);
        }

        public void RemoveFromCache(string key)
        {
            this.Cache.TryRemove(key, out object val);
        }

        private string HideIfAppropriate(string value, bool hideValue)
        {
            if (!hideValue) return value;
            if (value.Contains(".vault.azure.net/", StringComparison.InvariantCultureIgnoreCase)) return value;
            return "(set)";
        }

        public void Require(string key, string value, bool hideValue = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                this.Logger.LogError($"{key} is REQUIRED but missing.");
                throw new Exception($"{key} is REQUIRED but missing.");
            }
            else
            {
                string val = HideIfAppropriate(value, hideValue);
                this.Logger.LogDebug($"{key} = \"{val}\"");
            }
        }

        public void Require(string key, string[] values, bool hideValue = false)
        {
            if (values.Count(v => v.Trim().Length > 0) < 1)
            {
                this.Logger.LogError($"{key} is REQUIRED but missing.");
                throw new Exception($"{key} is REQUIRED but missing.");
            }
            else
            {
                string val = HideIfAppropriate(string.Join(", ", values), hideValue);
                this.Logger.LogDebug($"{key} = \"{val}\"");
            }
        }

        public void Require(string key, bool hideValue = false)
        {
            string value = this.ConfigProvider.Get(key);
            Require(key, value, hideValue);
        }

        public bool Optional(string key, string value, bool hideValue = false, bool hideIfEmpty = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (!hideIfEmpty) this.Logger.LogDebug($"{key} is \"(not-set)\".");
                return false;
            }
            else
            {
                string val = HideIfAppropriate(value, hideValue);
                this.Logger.LogDebug($"{key} = \"{val}\"");
                return true;
            }
        }

        public bool Optional(string key, string[] values, bool hideValue = false, bool hideIfEmpty = false)
        {
            if (values == null || values.Count(v => v.Trim().Length > 0) < 1)
            {
                if (!hideIfEmpty) this.Logger.LogDebug($"{key} is \"(not-set)\".");
                return false;
            }
            else
            {
                string val = HideIfAppropriate(string.Join(", ", values), hideValue);
                this.Logger.LogDebug($"{key} = \"{val}\"");
                return true;
            }
        }

        public bool Optional(string key, bool value, bool hideValue = false, bool hideIfEmpty = false)
        {
            string val = HideIfAppropriate(value.ToString(), hideValue);
            this.Logger.LogDebug($"{key} = \"{val}\"");
            return true;
        }

        public bool Optional(string key, bool hideValue = false, bool hideIfEmpty = false)
        {
            string value = this.ConfigProvider.Get(key);
            if (string.IsNullOrEmpty(value))
            {
                if (!hideIfEmpty) this.Logger.LogDebug($"{key} is \"(not-set)\".");
                return false;
            }
            else
            {
                string val = HideIfAppropriate(value, hideValue);
                this.Logger.LogDebug($"{key} = \"{val}\"");
                return true;
            }
        }

        public static LogLevel LogLevel
        {
            get => GetOnce("LOG_LEVEL").AsEnum<LogLevel>(() => LogLevel.Information);
        }

        public static bool DisableColors
        {
            get => GetOnce("DISABLE_COLORS").AsBool(() => false);
        }

        public static string AppConfig
        {
            get
            {
                var val = GetOnce("APPCONFIG");
                if (!string.IsNullOrEmpty(val))
                {
                    val = val.ToLower();
                    if (!val.Contains(".azconfig.io")) val += ".azconfig.io";
                }
                return val;
            }
        }

        public static string[] ConfigKeys
        {
            get
            {
                return GetOnce("CONFIG_KEYS").AsArray(() => null);
            }
        }

        public virtual AuthTypes AuthType(string type = null, Func<string, string> map = null)
        {
            // NOTE: this does not support getting AUTH_TYPE from Key Vault; it uses GetOnce() instead of Get()

            // check the cache
            var key = string.IsNullOrEmpty(type) ? "AUTH_TYPE" : $"AUTH_TYPE_{type.ToUpper()}";
            if (GetFromCache<AuthTypes>(key, out AuthTypes val)) return val;

            // get the value from env and convert to enum
            if (string.IsNullOrEmpty(type))
            {
                var str = GetOnce("AUTH_TYPE");
                val = str.AsEnum<AuthTypes>(() => AuthTypes.ManagedIdentity, map);
            }
            else
            {
                var str = GetOnce($"AUTH_TYPE_{type.ToUpper()}", "AUTH_TYPE");
                val = str.AsEnum<AuthTypes>(() => AuthTypes.ManagedIdentity, map);
            }

            // set the cache
            AddToCache<AuthTypes>(key, val);
            return val;

        }

        public virtual string TenantId(string type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                return GetOnce("TENANT_ID");
            }
            else
            {
                return GetOnce($"TENANT_ID_{type.ToUpper()}", "TENANT_ID");
            }
        }

        public virtual string ClientId(string type = null)
        {
            if (string.IsNullOrEmpty(type))
            {
                return GetOnce("CLIENT_ID");
            }
            else
            {
                return GetOnce($"CLIENT_ID_{type.ToUpper()}", "CLIENT_ID");
            }
        }

        public virtual async Task<string> ClientSecret(string type = null)
        {

            // determine the key to use
            var key = (type == null) ? "CLIENT_SECRET" : $"CLIENT_SECRET_{type.ToUpper()}";

            // check the cache
            if (GetFromCache<string>(key, out string val)) return val;

            // look for the value
            val = string.IsNullOrEmpty(type)
                ? GetOnce("CLIENT_SECRET")
                : GetOnce($"CLIENT_SECRET_{type.ToUpper()}", "CLIENT_SECRET");

            // resolve key-vault links
            val = await GetFromKeyVault(val);

            // add to the cache
            AddToCache(key, val);
            return val;

        }




    }


}

