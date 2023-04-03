using Parcs.HostAPI.Extensions.Functional;
using Parcs.Net;
using System.Reflection;

namespace Parcs.HostAPI.Services
{
    public class ArgumentsProvider : IArgumentsProvider
    {
        private readonly IDictionary<string, string> _arguments;

        public ArgumentsProvider(IDictionary<string, string> arguments)
        {
            _arguments = arguments;
        }

        public T Bind<T>() where T : class, new()
        {
            var @object = new T();

            var objectType = @object.GetType();

            foreach (var item in _arguments)
            {
                var itemProperty = objectType.GetProperty(item.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (itemProperty is not null)
                {
                    var itemValue = item.Value.ToObject(itemProperty.PropertyType);
                    itemProperty.SetValue(@object, itemValue, null);
                }
            }

            return @object;
        }

        public bool TryGet(string key, out string value) => _arguments.TryGetValue(key, out value);
    }
}