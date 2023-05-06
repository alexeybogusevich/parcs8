namespace Parcs.Core.Services.Interfaces
{
    public interface IModuleDirectoryPathBuilder
    {
        string Build();
        string Build(long moduleId);
    }
}