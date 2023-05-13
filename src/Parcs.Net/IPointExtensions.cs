namespace Parcs.Net
{
    public static class IPointExtensions
    {
        public static Task ExecuteClassAsync<TClass>(this IPoint point)
            where TClass : IModule
        {
            return point.ExecuteClassAsync(typeof(TClass).Assembly.GetName().Name, typeof(TClass).FullName);
        }
    }
}