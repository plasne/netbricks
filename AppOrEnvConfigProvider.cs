using System;
using Microsoft.Extensions.Configuration;

namespace NetBricks
{
    /// <summary>
    /// This class allows NetBricks to pull from an IConfiguration object before trying an environment variable.
    /// </summary>
    public class AppOrEnvConfigProvider : IConfigProvider
    {
        private readonly IConfiguration config;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppOrEnvConfigProvider"/> class.
        /// </summary>
        /// <param name="config">The configuration that may act as a source for parameters.</param>
        public AppOrEnvConfigProvider(IConfiguration config)
        {
            this.config = config;
        }

        /// <summary>
        /// Gets a value based on a key first trying the application configuration and then environment variable.
        /// </summary>
        /// <param name="key">The name of the configuration setting.</param>
        /// <returns>The value found for the key.</returns>
        public string Get(string key)
        {
            var val = this.config.GetValue<string>(key);
            if (!string.IsNullOrEmpty(val))
            {
                return val;
            }

            return Environment.GetEnvironmentVariable(key);
        }
    }
}