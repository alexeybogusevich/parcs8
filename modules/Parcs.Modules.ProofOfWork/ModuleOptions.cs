using Parcs.Net;

namespace Parcs.Modules.ProofOfWork
{
    public class ModuleOptions : IModuleOptions
    {
        public int Difficulty { get; set; } = 5;

        public string Prompt { get; set; } = "Hello world!";

        public long NonceBatchSize { get; set; } = 10_000_000;

        public long MaximumNonce { get; set; } = 80_000_000;

        public string OutputFilename { get; set; } = "Output.txt";
    }
}