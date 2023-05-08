namespace Parcs.Modules.ProofOfWork
{
    public class ModuleOutput
    {
        public double ElapsedSeconds { get; set; }

        public bool Found { get; set; }

        public int? ResultNonce { get; set; }

        public string ResultHash { get; set; }
    }
}