using Parcs.Net;

namespace Parcs.Modules.MatrixesMultiplication
{
    public class ModuleOptions : IModuleOptions
    {
        public int MatrixSize { get; set; } = 16;

        public bool SaveMatrixes { get; set; } = false;

        public string OutputFilename { get; set; } = "Output.txt";

        public string MatrixAOutputFilename { get; set; } = "MatrixA.txt";

        public string MatrixBOutputFilename { get; set; } = "MatrixB.txt";

        public string MatrixCOutputFilename { get; set; } = "MatrixC.txt";
    }
}