using Microsoft.AspNetCore.Components.Forms;

namespace Parcs.Portal.Models.Host.Requests
{
    public class CreateModuleHostRequest
    {
        public string Name { get; set; }

        public IEnumerable<IBrowserFile> BinaryFiles { get; set; }
    }
}