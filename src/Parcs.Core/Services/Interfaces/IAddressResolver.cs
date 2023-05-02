namespace Parcs.Core.Services.Interfaces
{
    public interface IAddressResolver
    {
        bool IsSameAddressAsHost(string url);
    }
}