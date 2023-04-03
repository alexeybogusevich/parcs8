namespace Parcs.HostAPI.Extensions.Functional
{
    public static class StringExtensions
    {
        public static object ToObject(this string value, Type type)
        {
            if (type.IsEnum)
            {
                return Enum.Parse(type, value, true);
            }

            return Convert.ChangeType(value, type);
        }
    }
}