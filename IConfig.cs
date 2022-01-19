using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json; // System.Text.Json was not deserializing properly

namespace NetBricks
{

    public interface IConfig
    {

        Task<Dictionary<string, string>> Load(string[] filters, bool useFullyQualifiedName = false);

        Task Apply(string[] filters = null);

        void Require(string key, string value, bool hideValue = false);

        void Require(string key, string[] values, bool hideValue = false);

        void Require(string key, bool value, bool hideValue = false);

        void Require(string key, int value, bool hideValue = false);

        void Require(string key, bool hideValue = false);

        bool Optional(string key, string value, bool hideValue = false, bool hideIfEmpty = false);

        bool Optional(string key, string[] values, bool hideValue = false, bool hideIfEmpty = false);

        bool Optional(string key, bool value, bool hideValue = false, bool hideIfEmpty = false);

        bool Optional(string key, int value, bool hideValue = false, bool hideIfEmpty = false);

        bool Optional(string key, bool hideValue = false, bool hideIfEmpty = false);

        Task<T> GetSecret<T>(string key, Func<string, T> convert = null, bool ignore404 = false);

        T Get<T>(string key, Func<string, T> convert = null);

        bool GetFromCache<T>(string key, out T val);

        void AddToCache<T>(string key, T val);

        Task<string> GetFromKeyVault(string posurl, bool ignore404 = false);

        AuthTypes AuthType(string type = null, Func<string, string> map = null);

        string TenantId(string type = null);

        string ClientId(string type = null);

        Task<string> ClientSecret(string type = null);

    }


}

