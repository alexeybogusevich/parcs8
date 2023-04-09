using System.Reflection;

namespace Parcs.Net
{
    public static class IArgumentsProviderExtensions
    {
        public static T Bind<T>(this IArgumentsProvider argumentsProvider) where T : class, new()
        {
            var @object = new T();

            var objectType = @object.GetType();

            foreach (var item in argumentsProvider.GetRaw())
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
    }
}