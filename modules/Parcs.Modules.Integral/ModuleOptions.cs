namespace Parcs.Modules.Integral
{
    public class ModuleOptions
    {
        public double XStart { get; set; } = 0;

        public double XEnd { get; set; } = Math.PI / 2;

        public double Precision { get; set; } = 0.00000001;

        public string OutputFilename { get; set; } = "Output.txt";
    }
}