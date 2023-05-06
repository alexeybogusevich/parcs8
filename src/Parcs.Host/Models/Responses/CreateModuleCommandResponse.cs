namespace Parcs.HostAPI.Models.Responses
{
    public class CreateModuleCommandResponse
    {
        public CreateModuleCommandResponse(long moduleId)
        {
            ModuleId = moduleId;
        }

        public long ModuleId { get; set; }
    }
}