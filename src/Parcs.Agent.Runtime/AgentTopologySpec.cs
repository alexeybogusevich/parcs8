namespace Parcs.Agent.Runtime;

/// <summary>
/// The shape of a layer's peer channels, parsed from run_layer's <c>topology</c> argument:
/// <c>none</c> (default), <c>ring</c>, <c>line</c>, <c>star</c>, <c>tree</c> or <c>tree:K</c>,
/// <c>hypercube</c>, <c>grid</c> or <c>grid:RxC</c>, <c>torus</c> or <c>torus:RxC</c>, <c>mesh</c>
/// and <c>custom</c> (edges given separately). Channels are undirected: one channel per
/// neighbouring pair, usable both ways.
/// </summary>
public sealed class AgentTopologySpec
{
    public const string None      = "none";
    public const string Ring      = "ring";
    public const string Line      = "line";
    public const string Star      = "star";
    public const string Tree      = "tree";
    public const string Hypercube = "hypercube";
    public const string Grid      = "grid";
    public const string Torus     = "torus";
    public const string Mesh      = "mesh";
    public const string Custom    = "custom";

    public static readonly IReadOnlyList<string> Kinds = [None, Ring, Line, Star, Tree, Hypercube, Grid, Torus, Mesh, Custom];

    private readonly IReadOnlyList<int[]> _edges;

    private AgentTopologySpec(string kind, int arity, int rows, int columns, IReadOnlyList<int[]> edges)
    {
        Kind    = kind;
        Arity   = arity;
        Rows    = rows;
        Columns = columns;
        _edges  = edges;
    }

    public string Kind { get; }

    /// <summary>Children per node of a tree.</summary>
    public int Arity { get; }

    /// <summary>Rows of a grid or torus; 0 until <see cref="Validate"/> picks them from the worker count.</summary>
    public int Rows { get; }

    public int Columns { get; }

    public IReadOnlyList<int[]> CustomEdges => _edges;

    public bool IsNone => Kind == None;

    public static AgentTopologySpec NoTopology { get; } = new(None, 0, 0, 0, []);

    /// <summary>
    /// Parses <paramref name="topology"/> (case-insensitive; null or empty is <c>none</c>, or
    /// <c>custom</c> when edges are given). Throws <see cref="FormatException"/> with a message fit
    /// to show the caller.
    /// </summary>
    public static AgentTopologySpec Parse(string? topology, IReadOnlyList<int[]>? edges = null)
    {
        var text = (topology ?? "").Trim().ToLowerInvariant();
        if (text.Length == 0)
            text = edges is { Count: > 0 } ? Custom : None;

        var colon = text.IndexOf(':');
        var kind = colon < 0 ? text : text[..colon];
        var argument = colon < 0 ? null : text[(colon + 1)..];

        if (!Kinds.Contains(kind))
            throw new FormatException($"Unknown topology '{topology}'; use one of: {string.Join(", ", Kinds)}.");

        if (kind != Custom && edges is { Count: > 0 })
            throw new FormatException($"topologyEdges are only used with topology 'custom', not '{kind}'.");

        switch (kind)
        {
            case Tree:
                var arity = 2;
                if (argument is not null && (!int.TryParse(argument, out arity) || arity < 1))
                    throw new FormatException($"'{topology}': the tree arity must be a positive integer, as in 'tree:3'.");
                return new AgentTopologySpec(Tree, arity, 0, 0, []);

            case Grid or Torus:
                if (argument is null)
                    return new AgentTopologySpec(kind, 0, 0, 0, []);
                var parts = argument.Split('x');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var rows) || !int.TryParse(parts[1], out var columns) ||
                    rows < 1 || columns < 1)
                {
                    throw new FormatException($"'{topology}': give the shape as rows x columns, as in '{kind}:4x8'.");
                }
                return new AgentTopologySpec(kind, 0, rows, columns, []);

            case Custom:
                if (edges is not { Count: > 0 })
                    throw new FormatException("topology 'custom' needs topologyEdges, a list of [i, j] worker pairs.");
                if (edges.Any(e => e is not { Length: 2 }))
                    throw new FormatException("Every entry of topologyEdges must be a pair [i, j].");
                return new AgentTopologySpec(Custom, 0, 0, 0, edges);

            default:
                if (argument is not null)
                    throw new FormatException($"Topology '{kind}' takes no argument (got '{topology}').");
                return new AgentTopologySpec(kind, 0, 0, 0, []);
        }
    }

    /// <summary>
    /// Checks the topology against the worker count and fills in a default grid shape.
    /// Returns the resolved spec, or throws <see cref="FormatException"/>.
    /// </summary>
    public AgentTopologySpec Validate(int count)
    {
        switch (Kind)
        {
            case Hypercube when count < 1 || (count & (count - 1)) != 0:
                throw new FormatException($"A hypercube needs a power-of-two number of workers, not {count}.");

            case Grid or Torus when Rows == 0:
                // Squarest shape: the largest divisor of count not above its square root.
                var rows = Math.Max(1, (int)Math.Sqrt(count));
                while (count % rows != 0) rows--;
                return new AgentTopologySpec(Kind, 0, rows, count / rows, []);

            case Grid or Torus when Rows * Columns != count:
                throw new FormatException($"A {Rows}x{Columns} {Kind} needs {Rows * Columns} workers, not {count}.");

            case Custom:
                foreach (var edge in _edges)
                {
                    if (edge[0] < 0 || edge[0] >= count || edge[1] < 0 || edge[1] >= count)
                        throw new FormatException($"Edge [{edge[0]}, {edge[1]}] names a worker outside 0..{count - 1}.");
                    if (edge[0] == edge[1])
                        throw new FormatException($"Edge [{edge[0]}, {edge[1]}] connects a worker to itself.");
                }
                break;
        }

        return this;
    }

    /// <summary>The neighbours of worker <paramref name="index"/> in ascending order, without itself.</summary>
    public IReadOnlyList<int> NeighborsOf(int index, int count)
    {
        var set = new SortedSet<int>();
        void Add(int? peer)
        {
            if (peer is { } p && p != index && p >= 0 && p < count)
                set.Add(p);
        }

        switch (Kind)
        {
            case Ring:
                Add((index + count - 1) % count);
                Add((index + 1) % count);
                break;
            case Line:
                Add(index - 1);
                Add(index + 1);
                break;
            case Star:
                if (index == 0)
                    for (var i = 1; i < count; i++) Add(i);
                else
                    Add(0);
                break;
            case Tree:
                Add(TreeParent(index));
                foreach (var child in TreeChildren(index, count)) Add(child);
                break;
            case Hypercube:
                for (var d = 0; (1 << d) < count; d++) Add(index ^ (1 << d));
                break;
            case Grid or Torus:
                var (north, south, west, east) = GridNeighbors(index);
                Add(north); Add(south); Add(west); Add(east);
                break;
            case Mesh:
                for (var i = 0; i < count; i++) Add(i);
                break;
            case Custom:
                foreach (var edge in _edges)
                {
                    if (edge[0] == index) Add(edge[1]);
                    else if (edge[1] == index) Add(edge[0]);
                }
                break;
        }

        return [.. set];
    }

    /// <summary>Every neighbouring pair (Low &lt; High) once.</summary>
    public IReadOnlyList<(int Low, int High)> Edges(int count) =>
        [.. Enumerable.Range(0, count).SelectMany(i => NeighborsOf(i, count).Where(j => j > i).Select(j => (i, j)))];

    internal int? TreeParent(int index) => Kind == Tree && index > 0 ? (index - 1) / Arity : null;

    internal IReadOnlyList<int> TreeChildren(int index, int count) => Kind == Tree
        ? [.. Enumerable.Range(Arity * index + 1, Arity).Where(c => c < count)]
        : [];

    internal (int? North, int? South, int? West, int? East) GridNeighbors(int index)
    {
        if (Kind is not (Grid or Torus) || Columns == 0)
            return (null, null, null, null);

        var (row, column) = (index / Columns, index % Columns);
        int? At(int r, int c)
        {
            if (Kind == Torus)
            {
                r = (r + Rows) % Rows;
                c = (c + Columns) % Columns;
            }
            return r >= 0 && r < Rows && c >= 0 && c < Columns && (r, c) != (row, column) ? r * Columns + c : null;
        }

        return (At(row - 1, column), At(row + 1, column), At(row, column - 1), At(row, column + 1));
    }

    /// <summary>Canonical text form, as stored with the layer: <c>ring</c>, <c>tree:3</c>, <c>grid:4x8</c>.</summary>
    public override string ToString() => Kind switch
    {
        Tree                        => $"{Tree}:{Arity}",
        Grid or Torus when Rows > 0 => $"{Kind}:{Rows}x{Columns}",
        _                           => Kind,
    };
}
