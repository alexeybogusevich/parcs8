using System.Reflection;

namespace Parcs.Net
{
    public static class IArgumentsProviderExtensions
    {
        public static T Bind<T>(this IArgumentsProvider argumentsProvider) where T : class, new()
        {
            var @object = new T();

            var objectType = @object.GetType();

            foreach (var argument in argumentsProvider.GetArguments())
            {
                var itemProperty = objectType.GetProperty(argument.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (itemProperty is not null)
                {
                    var itemValue = argument.Value.ToObject(itemProperty.PropertyType);
                    itemProperty.SetValue(@object, itemValue, null);
                }
            }

            return @object;
        }
    }
}