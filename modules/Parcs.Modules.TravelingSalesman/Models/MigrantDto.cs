namespace Parcs.Modules.TravelingSalesman.Models
{
    /// <summary>
    /// Lightweight, JSON-serializable DTO for transmitting individuals between islands during migration.
    /// <para>
    /// <see cref="Route"/> cannot be deserialized by <c>System.Text.Json</c> because it has no
    /// parameterless constructor (it requires a <c>cities</c> list and a <c>Random</c> instance that
    /// are not part of the JSON payload). This DTO carries only the two fields that actually need to
    /// cross the wire — the city permutation and the pre-computed tour distance. The receiving island
    /// reconstructs full <see cref="Route"/> objects from these fields using its own local cities list.
    /// </para>
    /// </summary>
    public class MigrantDto
    {
        /// <summary>City-index permutation representing the tour order.</summary>
        public List<int> Cities { get; set; } = new();

        /// <summary>Pre-computed total tour distance.</summary>
        public double TotalDistance { get; set; }
    }
}
