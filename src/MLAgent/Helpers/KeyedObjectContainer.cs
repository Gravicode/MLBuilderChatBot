using Microsoft.Extensions.Azure;
using System.Collections;

namespace MLAgent.Helpers
{
    public class KeyedObjectContainer
    {
        const string DefaultServiceName = "default";
        Dictionary<string,object> Objects { set; get; }
        public KeyedObjectContainer()
        {
            Objects = new();
        }

        public int GetTotalServices()
        {
            var total = 0;
            foreach(Dictionary<string, object> dict in Objects.Values)
            {
                total += dict.Count;
            }
            return total;
        }
        public bool ContainsKey<T>(string servicename) where T : class
        {
            var key = typeof(T).Name;
            if (Objects.ContainsKey(key))
            {
                var dict = Objects[key] as Dictionary<string, object>;
                return dict.ContainsKey(servicename);
            }
            return false;
        }
        public T Get<T>() where T : class
        {
            var key = typeof(T).Name;
            if (Objects.ContainsKey(key))
            {
                var dict = Objects[key] as Dictionary<string, object>;
                return dict[DefaultServiceName] as T;
            }
            throw new KeyNotFoundException($"Not found service name : {DefaultServiceName}");
        }
        public T Get<T>(string servicename) where T : class
        {
            var key = typeof(T).Name;
            if (Objects.ContainsKey(key))
            {
                var dict = Objects[key] as Dictionary<string, object>;
                return dict[servicename] as T;
            }
            throw new KeyNotFoundException($"Not found service name : {servicename}");
        }
        public void Register<T>(string servicename,object obj) where T : class
        {
            var key = typeof(T).Name;
            Dictionary<string, object> dict;
            if (Objects.ContainsKey(key))
            {
                dict  = Objects[key] as Dictionary<string, object>;
            }
            else
            {
                dict = new Dictionary<string, object>();
                Objects.Add(key, dict);
            }
            dict.Add(servicename, obj);
        }
        public void Register<T>(object obj) where T : class
        {
            
            var key = typeof(T).Name;
            Dictionary<string, object> dict;
            if (Objects.ContainsKey(key))
            {
                dict = Objects[key] as Dictionary<string, object>;
            }
            else
            {
                dict = new Dictionary<string, object>();
                Objects.Add(key, dict);
            }
            dict.Add(DefaultServiceName, obj);
        }
    }
}
