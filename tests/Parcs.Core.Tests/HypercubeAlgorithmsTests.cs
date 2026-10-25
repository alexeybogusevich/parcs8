using Parcs.Modules.Hypercube;

namespace Parcs.Core.Tests
{
    /// <summary>The hypercube module's algorithms, run on a cube simulated in memory.</summary>
    public class HypercubeAlgorithmsTests
    {
        [TestCase(0, 5)]
        [TestCase(1, 7)]
        [TestCase(3, 100)]
        [TestCase(4, 33)]
        [TestCase(6, 16)]
        public async Task BitonicSortOrdersTheWholeCube(int dimensions, int perNode)
        {
            var nodes = 1 << dimensions;
            var random = new Random(dimensions * 31 + perNode);
            var blocks = Enumerable.Range(0, nodes)
                .Select(_ => Enumerable.Range(0, perNode).Select(_ => random.Next(-50, 50)).ToArray())
                .ToArray();

            var cube = new SimulatedCube<int[]>(nodes);
            var results = await Task.WhenAll(Enumerable.Range(0, nodes).Select(i =>
                HypercubeAlgorithms.BitonicSortAsync(i, dimensions, blocks[i], (k, block) => cube.ExchangeAsync(i, k, block))));

            var concatenated = results.SelectMany(r => r.Block).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(concatenated, Is.EqualTo(blocks.SelectMany(b => b).Order().ToArray()));
                Assert.That(results.Select(r => r.Exchanges), Is.All.EqualTo(dimensions * (dimensions + 1) / 2));
            });
        }

        [Test]
        public async Task AllReduceGivesEveryNodeTheTotal()
        {
            const int dimensions = 5;
            var cube = new SimulatedCube<long>(1 << dimensions);
            var sums = await Task.WhenAll(Enumerable.Range(0, 1 << dimensions).Select(i =>
                HypercubeAlgorithms.AllReduceSumAsync(dimensions, i + 1, (k, value) => cube.ExchangeAsync(i, k, value))));

            Assert.That(sums, Is.All.EqualTo(32L * 33 / 2));
        }

        [TestCase(true, new[] { 1, 2, 3 })]
        [TestCase(false, new[] { 4, 5, 6 })]
        public void CompareSplitKeepsOneHalf(bool keepLow, int[] expected) =>
            Assert.That(HypercubeAlgorithms.CompareSplit([1, 4, 5], [2, 3, 6], keepLow), Is.EqualTo(expected));

        /// <summary>Pairs up the two ends of each edge: a node's exchange completes when its partner's does.</summary>
        private sealed class SimulatedCube<T>(int nodes)
        {
            private readonly Dictionary<(int, int, int), TaskCompletionSource<T>> _mailboxes = [];
            private readonly Dictionary<(int, int), int> _rounds = [];

            public Task<T> ExchangeAsync(int node, int dimension, T value)
            {
                var partner = HypercubeAlgorithms.Partner(node, dimension);
                Assert.That(partner, Is.LessThan(nodes));
                lock (_mailboxes)
                {
                    // The n-th exchange between two nodes matches the partner's n-th one.
                    var round = _rounds.TryGetValue((node, partner), out var r) ? r : 0;
                    _rounds[(node, partner)] = round + 1;
                    Mailbox(partner, node, round).SetResult(value);
                    return Mailbox(node, partner, round).Task;
                }
            }

            private TaskCompletionSource<T> Mailbox(int to, int from, int round)
            {
                if (!_mailboxes.TryGetValue((to, from, round), out var box))
                {
                    box = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _mailboxes[(to, from, round)] = box;
                }
                return box;
            }
        }
    }
}
