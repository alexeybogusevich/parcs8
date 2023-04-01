namespace Parcs.Net
{
    public static class IPointExtensions
    {
        public static Task ExecuteClassAsync<TClass>(this IPoint point)
        {
            return point.ExecuteClassAsync(typeof(TClass).Assembly.GetName().Name, typeof(TClass).FullName);
        }
    }
}