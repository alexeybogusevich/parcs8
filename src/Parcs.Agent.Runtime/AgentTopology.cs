namespace Parcs.Agent.Runtime;

/// <summary>
/// This worker's place in the layer's topology and its channels to the neighbours
/// (<c>input.Topology</c>). Members that do not apply to the topology are null or empty:
/// Left/Right for ring and line, Parent/Children for tree (worker 0 is the root) and star
/// (worker 0 is the hub), Dimensions/Partner for hypercube, Row/Column and North/South/West/East
/// for grid and torus. With topology <c>none</c>, Neighbors is empty.
/// </summary>
public sealed class AgentTopology
{
    private readonly AgentTopologySpec _spec;
    private readonly IReadOnlyDictionary<int, IAgentChannel> _channels;

    public AgentTopology(AgentTopologySpec spec, int index, int count, IReadOnlyDictionary<int, IAgentChannel> channels)
    {
        _spec     = spec;
        _channels = channels;
        Index     = index;
        Count     = count;
        Neighbors = spec.NeighborsOf(index, count);
    }

    public static AgentTopology Unconnected(int index, int count) =>
        new(AgentTopologySpec.NoTopology, index, count, new Dictionary<int, IAgentChannel>());

    /// <summary>none, ring, line, star, tree, hypercube, grid, torus, mesh or custom.</summary>
    public string Kind => _spec.Kind;

    /// <summary>Canonical form with its parameters, e.g. tree:2 or grid:4x8.</summary>
    public string Description => _spec.ToString();

    public int Index { get; }

    public int Count { get; }

    /// <summary>Worker indices this worker has a channel to, ascending.</summary>
    public IReadOnlyList<int> Neighbors { get; }

    /// <summary>The channel to neighbour <paramref name="peer"/>.</summary>
    public IAgentChannel this[int peer] => _channels.TryGetValue(peer, out var channel)
        ? channel
        : throw new ArgumentException(
            $"Worker {Index} has no channel to worker {peer} in topology {Description}; its neighbours are [{string.Join(", ", Neighbors)}].",
            nameof(peer));

    /// <summary>Channels to every neighbour, in the order of <see cref="Neighbors"/>.</summary>
    public IReadOnlyList<IAgentChannel> Channels => [.. Neighbors.Select(p => this[p])];

    /// <summary>Ring: the previous worker (wrapping). Line: the previous worker, null for worker 0.</summary>
    public int? Left => _spec.Kind switch
    {
        AgentTopologySpec.Ring when Count > 1 => (Index + Count - 1) % Count,
        AgentTopologySpec.Line when Index > 0 => Index - 1,
        _ => null,
    };

    /// <summary>Ring: the next worker (wrapping). Line: the next worker, null for the last one.</summary>
    public int? Right => _spec.Kind switch
    {
        AgentTopologySpec.Ring when Count > 1         => (Index + 1) % Count,
        AgentTopologySpec.Line when Index < Count - 1 => Index + 1,
        _ => null,
    };

    /// <summary>Tree: the parent ((i - 1) / arity), null for the root. Star: worker 0 for every other worker.</summary>
    public int? Parent => _spec.Kind == AgentTopologySpec.Star
        ? (Index > 0 ? 0 : null)
        : _spec.TreeParent(Index);

    /// <summary>Tree: the children (arity * i + 1 ...). Star: every other worker, for worker 0.</summary>
    public IReadOnlyList<int> Children => _spec.Kind == AgentTopologySpec.Star
        ? (Index == 0 ? Neighbors : [])
        : _spec.TreeChildren(Index, Count);

    /// <summary>Hypercube: log2 of the worker count.</summary>
    public int Dimensions => _spec.Kind == AgentTopologySpec.Hypercube ? System.Numerics.BitOperations.Log2((uint)Count) : 0;

    /// <summary>Hypercube: the neighbour across <paramref name="dimension"/> (Index XOR 2^dimension).</summary>
    public int Partner(int dimension) => _spec.Kind == AgentTopologySpec.Hypercube && dimension >= 0 && dimension < Dimensions
        ? Index ^ (1 << dimension)
        : throw new ArgumentOutOfRangeException(nameof(dimension),
            $"Partner needs a hypercube and a dimension in 0..{Dimensions - 1} (topology {Description}).");

    public int Rows => _spec.Rows;

    public int Columns => _spec.Columns;

    /// <summary>Grid/torus row of this worker (Index / Columns).</summary>
    public int Row => Columns > 0 ? Index / Columns : 0;

    /// <summary>Grid/torus column of this worker (Index % Columns).</summary>
    public int Column => Columns > 0 ? Index % Columns : 0;

    public int? North => _spec.GridNeighbors(Index).North;

    public int? South => _spec.GridNeighbors(Index).South;

    public int? West => _spec.GridNeighbors(Index).West;

    public int? East => _spec.GridNeighbors(Index).East;
}
