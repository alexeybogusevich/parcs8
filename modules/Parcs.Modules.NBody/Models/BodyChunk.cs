namespace Parcs.Modules.NBody.Models
{
    /// <summary>
    /// Structure-of-arrays layout (one float[] per component, rather than an array of body
    /// structs) — the natural GPU-friendly layout, and just as convenient on the CPU side, so
    /// both variants use the identical wire format.
    /// </summary>
    public class BodyChunk
    {
        public float[] X { get; set; }
        public float[] Y { get; set; }
        public float[] Z { get; set; }
        public float[] VX { get; set; }
        public float[] VY { get; set; }
        public float[] VZ { get; set; }
    }
}
