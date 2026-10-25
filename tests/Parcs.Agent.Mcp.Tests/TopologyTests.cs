using Parcs.Agent.Runtime;

namespace Parcs.Agent.Mcp.Tests;

public class TopologyTests
{
    private static int[] Neighbors(string topology, int index, int count, IReadOnlyList<int[]>? edges = null) =>
        [.. AgentTopologySpec.Parse(topology, edges).Validate(count).NeighborsOf(index, count)];

    [TestCase("ring", 0, 5, new[] { 1, 4 })]
    [TestCase("ring", 1, 2, new[] { 0 })]
    [TestCase("ring", 0, 1, new int[0])]
    [TestCase("line", 0, 4, new[] { 1 })]
    [TestCase("line", 2, 4, new[] { 1, 3 })]
    [TestCase("star", 0, 4, new[] { 1, 2, 3 })]
    [TestCase("star", 2, 4, new[] { 0 })]
    [TestCase("tree", 0, 7, new[] { 1, 2 })]
    [TestCase("tree", 2, 7, new[] { 0, 5, 6 })]
    [TestCase("tree:3", 1, 13, new[] { 0, 4, 5, 6 })]
    [TestCase("tree", 3, 5, new[] { 1 })]
    [TestCase("hypercube", 5, 8, new[] { 1, 4, 7 })]
    [TestCase("grid:2x3", 4, 6, new[] { 1, 3, 5 })]
    [TestCase("grid:2x3", 0, 6, new[] { 1, 3 })]
    [TestCase("torus:3x3", 0, 9, new[] { 1, 2, 3, 6 })]
    [TestCase("torus:2x2", 0, 4, new[] { 1, 2 })]
    [TestCase("mesh", 1, 4, new[] { 0, 2, 3 })]
    [TestCase(null, 0, 4, new int[0])]
    [TestCase("NONE", 0, 4, new int[0])]
    public void NeighboursFollowTheTopology(string? topology, int index, int count, int[] expected) =>
        Assert.That(Neighbors(topology!, index, count), Is.EqualTo(expected));

    [Test]
    public void CustomEdgesAreUndirectedAndDeduplicated()
    {
        int[][] edges = [[0, 1], [1, 0], [2, 1]];
        Assert.Multiple(() =>
        {
            Assert.That(Neighbors("custom", 1, 3, edges), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(Neighbors("", 0, 3, edges), Is.EqualTo(new[] { 1 }), "edges alone imply custom");
        });
    }

    [Test]
    public void NeighbourRelationIsSymmetricForEveryKind()
    {
        foreach (var (topology, count) in new[] { ("ring", 6), ("line", 5), ("star", 5), ("tree:3", 11), ("hypercube", 16),
                     ("grid", 12), ("torus", 12), ("mesh", 5) })
        {
            var spec = AgentTopologySpec.Parse(topology).Validate(count);
            for (var i = 0; i < count; i++)
                foreach (var j in spec.NeighborsOf(i, count))
                    Assert.That(spec.NeighborsOf(j, count), Does.Contain(i), $"{topology}: {i}-{j}");
        }
    }

    [TestCase("grid", 12, "grid:3x4")]
    [TestCase("grid", 7, "grid:1x7")]
    [TestCase("torus", 16, "torus:4x4")]
    [TestCase("tree", 3, "tree:2")]
    [TestCase("Ring", 3, "ring")]
    public void CanonicalFormFillsInDefaults(string topology, int count, string expected) =>
        Assert.That(AgentTopologySpec.Parse(topology).Validate(count).ToString(), Is.EqualTo(expected));

    [TestCase("hypercube", 6, "power-of-two")]
    [TestCase("grid:2x3", 8, "needs 6 workers")]
    [TestCase("tree:0", 4, "positive integer")]
    [TestCase("grid:2-3", 6, "rows x columns")]
    [TestCase("ring:3", 3, "takes no argument")]
    [TestCase("donut", 3, "Unknown topology")]
    [TestCase("custom", 3, "needs topologyEdges")]
    public void InvalidTopologiesAreRejectedWithAReason(string topology, int count, string reason) =>
        Assert.That(() => AgentTopologySpec.Parse(topology).Validate(count),
            Throws.InstanceOf<FormatException>().With.Message.Contains(reason));

    [Test]
    public void CustomEdgesOutsideTheLayerAreRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => AgentTopologySpec.Parse("custom", [[0, 3]]).Validate(3),
                Throws.InstanceOf<FormatException>().With.Message.Contains("outside"));
            Assert.That(() => AgentTopologySpec.Parse("custom", [[1, 1]]).Validate(3),
                Throws.InstanceOf<FormatException>().With.Message.Contains("itself"));
            Assert.That(() => AgentTopologySpec.Parse("ring", [[0, 1]]),
                Throws.InstanceOf<FormatException>().With.Message.Contains("only used with topology 'custom'"));
        });
    }

    [Test]
    public void EdgesListsEveryPairOnce() =>
        Assert.That(AgentTopologySpec.Parse("ring").Validate(4).Edges(4), Is.EqualTo(new[] { (0, 1), (0, 3), (1, 2), (2, 3) }));

    [Test]
    public void TopologyHelpersNameTheRightNeighbours()
    {
        var none = new Dictionary<int, IAgentChannel>();
        var ring = new AgentTopology(AgentTopologySpec.Parse("ring").Validate(5), 0, 5, none);
        var line = new AgentTopology(AgentTopologySpec.Parse("line").Validate(5), 4, 5, none);
        var tree = new AgentTopology(AgentTopologySpec.Parse("tree").Validate(7), 2, 7, none);
        var star = new AgentTopology(AgentTopologySpec.Parse("star").Validate(4), 0, 4, none);
        var cube = new AgentTopology(AgentTopologySpec.Parse("hypercube").Validate(8), 5, 8, none);
        var grid = new AgentTopology(AgentTopologySpec.Parse("grid:3x4").Validate(12), 5, 12, none);

        Assert.Multiple(() =>
        {
            Assert.That((ring.Left, ring.Right), Is.EqualTo(((int?)4, (int?)1)));
            Assert.That((line.Left, line.Right), Is.EqualTo(((int?)3, (int?)null)));
            Assert.That((tree.Parent, tree.Children), Is.EqualTo(((int?)0, new[] { 5, 6 })));
            Assert.That((star.Parent, star.Children), Is.EqualTo(((int?)null, new[] { 1, 2, 3 })));
            Assert.That((cube.Dimensions, cube.Partner(0), cube.Partner(2)), Is.EqualTo((3, 4, 1)));
            Assert.That((grid.Row, grid.Column, grid.North, grid.South, grid.West, grid.East),
                Is.EqualTo((1, 1, (int?)1, (int?)9, (int?)4, (int?)6)));
            Assert.That(() => ring[2], Throws.ArgumentException.With.Message.Contains("no channel to worker 2"));
            Assert.That(() => ring.Partner(0), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }
}
