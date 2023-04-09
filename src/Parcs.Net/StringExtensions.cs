using System.ComponentModel;

namespace Parcs.Net
{
    internal static class StringExtensions
    {
        public static object ToObject(this string value, Type type)
        {
            if (type.IsEnum)
            {
                return Enum.Parse(type, value, true);
            }

            if (Nullable.GetUnderlyingType(type) is not null)
            {
                return TypeDescriptor.GetConverter(type).ConvertFrom(value);
            }

            return Convert.ChangeType(value, type);
        }
    }
}