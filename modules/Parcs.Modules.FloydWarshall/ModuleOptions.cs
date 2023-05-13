namespace Parcs.Modules.FloydWarshall
{
    public class ModuleOptions
    {
        public int VerticesNumber { get; set; }

        public bool SaveMatrixes { get; set; }

        public string InputFile { get; set; }

        public string OutputFile { get; set; } = "Output.txt";
    }
}