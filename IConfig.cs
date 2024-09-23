using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;

namespace NetBricks
{
    public interface IConfig
    {
        /// <summary>
        /// The default Azure credential used to authenticate to Azure services.
        /// </summary>
        DefaultAzureCredential DefaultAzureCredential { get; }

        /// <summary>
        /// Loads configuration key-value pairs from Azure App Configuration based on the specified filters.
        /// </summary>
        /// <param name="filters">An array of key filters to retrieve specific configuration values.</param>
        /// <param name="useFullyQualifiedName">
        /// A boolean indicating whether to use the fully qualified name for the keys.
        /// If false, the key will be the last segment after splitting by ':'.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a dictionary
        /// with the configuration key-value pairs.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the identity is not authorized to access the AppConfig or if there is an HTTP error.
        /// </exception
        Task<Dictionary<string, string>> Load(string[] filters, bool useFullyQualifiedName = false);

        /// <summary>
        /// Applies configuration settings by loading key-value pairs from Azure App Configuration
        /// and setting them as environment variables.
        /// </summary>
        /// <param name="filters">
        /// An optional array of key filters to retrieve specific configuration values.
        /// If null, the default configuration keys will be used.
        /// </param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="Exception">
        /// Thrown if there is an error loading the configuration values.
        /// </exception>
        Task Apply(string[] filters = null);

        /// <summary>
        /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        void Require(string key, string value, bool hideValue = false);

        /// <summary>
        /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <param name="hideValue"></param>
        void Require(string key, string[] values, bool hideValue = false);

        /// <summary>
        /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        void Require(string key, bool value, bool hideValue = false);

        /// <summary>
        /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        void Require(string key, int value, bool hideValue = false);

        /// <summary>
        /// Requires a configuration value to be set. If the value is not set, an exception is thrown.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hideValue"></param>
        void Require(string key, bool hideValue = false);

        /// <summary>
        /// Makes a configuration value optional.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        /// <param name="hideIfEmpty"></param>
        /// <returns>
        /// A boolean indicating whether the value is set.
        /// </returns>
        bool Optional(string key, string value, bool hideValue = false, bool hideIfEmpty = false);

        /// <summary>
        /// Makes a configuration value optional.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        /// <param name="hideIfEmpty"></param>
        /// <returns>
        /// A boolean indicating whether the value is set.
        /// </returns>
        bool Optional(string key, string[] values, bool hideValue = false, bool hideIfEmpty = false);

        /// <summary>
        /// Makes a configuration value optional.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        /// <param name="hideIfEmpty"></param>
        /// <returns>
        /// A boolean indicating whether the value is set.
        /// </returns>
        bool Optional(string key, bool value, bool hideValue = false, bool hideIfEmpty = false);

        /// <summary>
        /// Makes a configuration value optional.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        /// <param name="hideIfEmpty"></param>
        /// <returns>
        /// A boolean indicating whether the value is set.
        /// </returns>
        bool Optional(string key, int value, bool hideValue = false, bool hideIfEmpty = false);

        /// <summary>
        /// Makes a configuration value optional.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="hideValue"></param>
        /// <param name="hideIfEmpty"></param>
        /// <returns>
        /// A boolean indicating whether the value is set.
        /// </returns>
        bool Optional(string key, bool hideValue = false, bool hideIfEmpty = false);

        /// <summary>
        /// This Get() method supports pulling from cache, getting the value from the environment
        /// variables, converting to the specified datatype, and storing in a variable.
        /// </summary>
        /// <typeparam name="T">The type to which the secret value should be converted.</typeparam>
        /// <param name="key">The key of the secret to retrieve.</param>
        /// <param name="convert">An optional function to convert the secret value from a string to the desired type. If null, the default conversion will be used.</param>
        /// <param name="ignore404">A boolean indicating whether to ignore a 404 (not found) error. If true, the method will return the default value of T in case of a 404 error.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the secret value converted to the specified type.</returns>
        Task<T> GetSecret<T>(string key, Func<string, T> convert = null, bool ignore404 = false);

        /// <summary>
        /// Retrieves a configuration value associated with the specified key.
        /// </summary>
        /// <typeparam name="T">The type to which the configuration value should be converted.</typeparam>
        /// <param name="key">The key of the configuration value to retrieve.</param>
        /// <param name="convert">
        /// An optional function to convert the configuration value from a string to the desired type.
        /// If null, the default conversion will be used (only applicable if T is string).
        /// </param>
        /// <returns>The configuration value converted to the specified type.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the convert function is null and the type T is not string.
        /// </exception>
        T Get<T>(string key, Func<string, T> convert = null);

        /// <summary>
        /// Attempts to retrieve a value from the cache associated with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve from the cache.</typeparam>
        /// <param name="key">The key of the value to retrieve from the cache.</param>
        /// <param name="val">
        /// When this method returns, contains the value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the val parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the cache contains an element with the specified key; otherwise, false.
        /// </returns>
        bool GetFromCache<T>(string key, out T val);

        /// <summary>
        /// Adds a value to the cache associated with the specified key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="val"></param>
        void AddToCache<T>(string key, T val);

        /// <summary>
        /// Retrieves a secret from Azure Key Vault.
        /// </summary>
        /// <param name="posurl">The URL of the Key Vault secret.</param>
        /// <param name="ignore404">If true, returns an empty string when a 404 (Not Found) status is encountered; otherwise, throws an exception.</param>
        /// <returns>The value of the secret from Key Vault, or the original URL if it is not a Key Vault URL.</returns>
        /// <exception cref="Exception">Thrown when the HTTP request to Key Vault fails with a status code other than 404 (if ignore404 is true).</exception>
        /// <remarks>
        /// This method checks if the provided URL is a valid Azure Key Vault URL. If it is, it retrieves an access token using the default Azure credentials,
        /// sends an HTTP GET request to the Key Vault, and returns the secret value. If the URL is not a Key Vault URL, it simply returns the original URL.
        /// </remarks>
        Task<string> GetFromKeyVault(string posurl, bool ignore404 = false);
    }
}

