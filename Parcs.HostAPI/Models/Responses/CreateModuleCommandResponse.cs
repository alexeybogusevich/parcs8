using Parcs.HostAPI.Models.Domain;

namespace Parcs.HostAPI.Models.Responses
{
    public class CreateModuleCommandResponse
    {
        public CreateModuleCommandResponse(Guid moduleId)
        {
            ModuleId = moduleId;
        }

        public Guid ModuleId { get; set; }
    }
}