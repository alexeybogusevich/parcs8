namespace Parcs.Modules.NBody.Models
{
    /// <summary>
    /// The full (all TotalBodies) position snapshot gathered by the Main module each step and
    /// broadcast back out so every worker can compute pairwise forces against every body, not
    /// just its own chunk.
    /// </summary>
    public class PositionSnapshot
    {
        public float[] X { get; set; }
        public float[] Y { get; set; }
        public float[] Z { get; set; }
    }
}
