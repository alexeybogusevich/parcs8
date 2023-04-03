using System.ComponentModel.DataAnnotations;

namespace Parcs.Modules.FloydWarshall
{
    public class ModuleOptions
    {
        public string InputFile { get; set; }

        public string OutputFile { get; set; }

        public int PointsCount { get; set; }
    }
}