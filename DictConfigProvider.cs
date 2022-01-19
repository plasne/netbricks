using System.Collections.Generic;

namespace NetBricks
{
    public class DictConfigProvider : IConfigProvider
    {
        private Dictionary<string, string> values = new Dictionary<string, string>();

        public string Get(string key)
        {
            return this.values.ContainsKey(key) ? this.values[key] : null;
        }

        public void Add(string key, string value)
        {
            this.values[key] = value;
        }
    }
}