using System.Collections.Concurrent;
using System.Threading.Channels;
using Parcs.Modules.Ring;
using Parcs.Modules.Tree;
using Parcs.Modules.Tree.Models;

namespace Parcs.Core.Tests
{
    /// <summary>The ring and tree modules' algorithms, run on topologies simulated in memory.</summary>
    public class RingAndTreeAlgorithmsTests
    {
        [TestCase(1, 10)]
        [TestCase(2, 9)]
        [TestCase(3, 3)]
        [TestCase(5, 101)]
        [TestCase(8, 1000)]
        public async Task RingAllReduceGivesEveryNodeTheElementwiseSum(int nodes, int length)
        {
            var random = new Random(nodes * 7 + length);
            var vectors = Enumerable.Range(0, nodes)
                .Select(_ => Enumerable.Range(0, length).Select(_ => (long)random.Next(-1000, 1000)).ToArray())
                .ToArray();
            var expected = Enumerable.Range(0, length).Select(k => vectors.Sum(v => v[k])).ToArray();

            // links[i] carries what node i-1 sends to its right neighbour i.
            var links = Enumerable.Range(0, nodes).Select(_ => Channel.CreateUnbounded<long[]>()).ToArray();
            var results = await Task.WhenAll(Enumerable.Range(0, nodes).Select(i =>
                RingAlgorithms.AllReduceSumAsync(i, nodes, vectors[i], async chunk =>
                {
                    await links[RingAlgorithms.Right(i, nodes)].Writer.WriteAsync(chunk);
                    return await links[i].Reader.ReadAsync();
                })));

            Assert.Multiple(() =>
            {
                foreach (var (vector, steps) in results)
                {
                    Assert.That(vector, Is.EqualTo(expected));
                    Assert.That(steps, Is.EqualTo(2 * (nodes - 1)));
                }
                Assert.That(results.Select(r => RingAlgorithms.Checksum(r.Vector)).Distinct().Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public void RingChunksTileTheVector()
        {
            var bounds = Enumerable.Range(0, 7).Select(c => RingAlgorithms.Bounds(100, c, 7)).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(bounds[0].Start, Is.Zero);
                Assert.That(bounds.Sum(b => b.Length), Is.EqualTo(100));
                Assert.That(bounds.Zip(bounds.Skip(1)).All(p => p.First.Start + p.First.Length == p.Second.Start), Is.True);
            });
        }

        [TestCase(1, 2, 1_000)]
        [TestCase(7, 2, 100_000)]
        [TestCase(10, 3, 54_321)]
        [TestCase(5, 1, 30_000)]
        [TestCase(12, 4, 1_000_003)]
        public async Task TreeScatterReduceCountsEveryPrimeOnce(int nodes, int arity, long limit)
        {
            var tree = new SimulatedTree();
            var outcomes = await Task.WhenAll(Enumerable.Range(0, nodes).Select(i =>
                Task.Run(() => TreeAlgorithms.ScatterReduceAsync(i, nodes, arity, (2, limit), tree.LinksOf(i, arity), TreeAlgorithms.CountPrimes))));

            var expected = TreeAlgorithms.CountPrimes((2, limit));
            var segments = outcomes.Select(o => o.Segment).OrderBy(s => s.Start).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(outcomes[0].Subtree, Is.EqualTo(expected));
                Assert.That(outcomes.Sum(o => o.Local.Count), Is.EqualTo(expected.Count));
                Assert.That(segments[0].Start, Is.EqualTo(2));
                Assert.That(segments[^1].End, Is.EqualTo(limit));
                Assert.That(segments.Zip(segments.Skip(1)).All(p => p.First.End == p.Second.Start), Is.True);
            });
        }

        [Test]
        public void SieveMatchesKnownPrimeCounts() => Assert.Multiple(() =>
        {
            Assert.That(TreeAlgorithms.CountPrimes((2, 100)), Is.EqualTo(new PrimeTally(25, 97)));
            Assert.That(TreeAlgorithms.CountPrimes((2, 10_000_000)).Count, Is.EqualTo(664_579));
            Assert.That(TreeAlgorithms.CountPrimes((90, 97)), Is.EqualTo(new PrimeTally(0, 0)));
        });

        [Test]
        public void TreeShapeFollowsTheArity() => Assert.Multiple(() =>
        {
            Assert.That(TreeAlgorithms.Children(1, 13, 3), Is.EqualTo(new[] { 4, 5, 6 }));
            Assert.That(TreeAlgorithms.Parent(6, 3), Is.EqualTo(1));
            Assert.That(TreeAlgorithms.Parent(0, 3), Is.Null);
            Assert.That(TreeAlgorithms.Depth(12, 3), Is.EqualTo(2));
            Assert.That(TreeAlgorithms.SubtreeSize(1, 7, 2), Is.EqualTo(3));
        });

        /// <summary>One queue per directed tree edge.</summary>
        private sealed class SimulatedTree
        {
            private readonly ConcurrentDictionary<(int From, int To), Channel<object>> _edges = new();

            public ITreeLinks LinksOf(int index, int arity) => new Links(this, index, TreeAlgorithms.Parent(index, arity));

            private Channel<object> Edge(int from, int to) => _edges.GetOrAdd((from, to), _ => Channel.CreateUnbounded<object>());

            private sealed class Links(SimulatedTree tree, int index, int? parent) : ITreeLinks
            {
                public async Task<(long Start, long End)> ReceiveRangeFromParentAsync() =>
                    ((long, long))await tree.Edge(parent!.Value, index).Reader.ReadAsync();

                public Task SendRangeToChildAsync(int child, (long Start, long End) range) =>
                    tree.Edge(index, child).Writer.WriteAsync(range).AsTask();

                public async Task<PrimeTally> ReceiveTallyFromChildAsync(int child) =>
                    (PrimeTally)await tree.Edge(child, index).Reader.ReadAsync();

                public Task SendTallyToParentAsync(PrimeTally tally) =>
                    tree.Edge(index, parent!.Value).Writer.WriteAsync(tally).AsTask();
            }
        }
    }
}
