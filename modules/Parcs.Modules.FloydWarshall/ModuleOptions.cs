using Parcs.Net;

namespace Parcs.Modules.FloydWarshall
{
    public class ModuleOptions : IModuleOptions
    {
        public int VerticesNumber { get; set; } = 16;

        public bool SaveMatrixes { get; set; } = false;

        public string InputFile { get; set; }

        public string OutputFile { get; set; } = "Output.txt";
    }
}