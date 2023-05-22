using Microsoft.AspNetCore.Components.Forms;

namespace Parcs.Portal.Models.Host.Requests
{
    public class CreateJobHostRequest
    {
        public long ModuleId { get; set; }

        public IEnumerable<IBrowserFile> InputFiles { get; set; }

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }
    }
}