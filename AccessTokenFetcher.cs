using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;

namespace NetBricks
{

    public enum AuthTypes
    {
        Service,
        Token,
        ManagedIdentity
    }

    public static class AddAccessTokenFetcherConfiguration
    {

        public static void AddAccessTokenFetcher(this IServiceCollection services)
        {
            services.TryAddSingleton<IAccessTokenFetcher, AccessTokenFetcher>();
        }

    }

    public class AccessTokenFetcher : IAccessTokenFetcher
    {

        public AccessTokenFetcher(
            ILogger<AccessTokenFetcher> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider
        )
        {
            this.Logger = logger;
            this.HttpClient = httpClientFactory.CreateClient("netbricks");
            this.AccessTokens = new ConcurrentDictionary<string, string>();
            this.ServiceProvider = serviceProvider;
        }

        private ILogger<AccessTokenFetcher> Logger { get; }
        private HttpClient HttpClient { get; }
        private IConfig config;
        private IConfig Config
        {
            get
            {
                if (this.config != null) return this.config;
                this.config = this.ServiceProvider.GetService<IConfig>();
                return this.config;
            }
        }
        private ConcurrentDictionary<string, string> AccessTokens { get; }
        private IServiceProvider ServiceProvider { get; }

        private async Task<string> GetAccessTokenViaAzureServiceTokenProvider(string resourceId)
        {
            try
            {
                var tokenProvider = new AzureServiceTokenProvider();
                string accessToken = await tokenProvider.GetAccessTokenAsync(resourceId);
                Logger.LogDebug($"GetAccessTokenViaAzureServiceTokenProvider: succeeded in getting a token for \"{resourceId}\".");
                return accessToken;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAccessTokenViaAzureServiceTokenProvider: failed in getting a token for \"{resourceId}\"...");
                throw;
            }
        }

        private async Task<string> GetAccessTokenViaManagedIdentityEndpoint(string resourceId)
        {
            try
            {
                using (var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"http://169.254.169.254/metadata/identity/oauth2/token?resource={resourceId}"),
                    Method = HttpMethod.Get
                })
                {
                    using (var response = await this.HttpClient.SendAsync(request))
                    {
                        var raw = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode) throw new Exception($"GetAccessTokenViaManagedIdentityEndpoint: GetAccessTokenViaManagedIdentityEndpoint: HTTP {(int)response.StatusCode} - {raw}");
                        dynamic json = JObject.Parse(raw);
                        Logger.LogDebug($"GetAccessTokenViaManagedIdentityEndpoint: http://169.254.169.254/metadata succeeded in getting a token for \"{resourceId}\".");
                        return (string)json.access_token;
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAccessTokenViaManagedIdentityEndpoint: http://169.254.169.254/metadata failed in getting a token for \"{resourceId}\"...");
                throw;
            }
        }

        private async Task<string> GetAccessTokenViaServiceAccount(string resourceId, string type)
        {
            try
            {

                // get the creds
                var tenant = this.Config.TenantId(type);
                var client = this.Config.ClientId(type);
                var secret = await this.Config.ClientSecret(type);

                // builder
                var app = ConfidentialClientApplicationBuilder
                    .Create(client)
                    .WithTenantId(tenant)
                    .WithClientSecret(secret)
                    .Build();

                // get an access token
                string[] scopes = new string[] { $"offline_access {resourceId}/.default" };
                var acquire = await app.AcquireTokenForClient(scopes).ExecuteAsync();
                Logger.LogDebug($"GetAccessTokenViaServiceAccount: succeeded in getting a token for \"{resourceId}\".");
                return acquire.AccessToken;

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAccessTokenViaServiceAccount: failed in getting a token for \"{resourceId}\"...");
                throw;
            }
        }

        public async Task<string> GetAccessTokenOnce(string resourceId, string type = null)
        {

            // get by type
            var authType = this.Config.AuthType(type);
            switch (authType)
            {
                case AuthTypes.Service:
                    {
                        var token = await this.GetAccessTokenViaServiceAccount(resourceId, type);
                        return token;
                    }
                case AuthTypes.Token:
                    {
                        var token = await this.GetAccessTokenViaAzureServiceTokenProvider(resourceId);
                        return token;
                    }
                case AuthTypes.ManagedIdentity:
                    {
                        var token = await this.GetAccessTokenViaManagedIdentityEndpoint(resourceId);
                        return token;
                    }
            }

            // throw exception
            throw new Exception($"GetAccessToken: type \"{type}\" could not be processed.");

        }

        public async Task<string> GetAccessToken(string resourceId, string type = null)
        {

            // try the cache first
            string key = string.IsNullOrEmpty(type) ? "$DEFAULT" : type.ToUpper();
            if (AccessTokens.TryGetValue(key, out string cached)) return cached;

            // get it once and cache it
            var token = await GetAccessTokenOnce(resourceId, type);
            AccessTokens.TryAdd(key, token);
            return token;

        }

    }

}