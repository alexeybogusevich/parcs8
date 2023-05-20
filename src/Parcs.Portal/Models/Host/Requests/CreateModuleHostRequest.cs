namespace Parcs.Portal.Models.Host.Requests
{
    public class CreateModuleHostRequest
    {
        public string Name { get; set; }

        public List<IFormFile> BinaryFiles { get; set; }
    }
}