namespace Parcs.Shared.Services.Interfaces
{
    public interface IModuleDirectoryPathBuilder
    {
        string Build();
        string Build(Guid moduleId);
    }
}