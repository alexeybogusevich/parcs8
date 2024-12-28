namespace Parcs.Host.Models.Responses
{
    public class CreateModuleCommandResponse(long moduleId)
    {
        public long ModuleId { get; set; } = moduleId;
    }
}