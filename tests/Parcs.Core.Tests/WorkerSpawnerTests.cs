using Parcs.Modules.AgentRunner;

namespace Parcs.Core.Tests
{
    /// <summary>The spawn limits, which are checked before any point is created.</summary>
    public class WorkerSpawnerTests
    {
        private static WorkerSpawner Spawner(int depth, int maxDepth, int budget) =>
            new(null!, depth, maxDepth, budget, "Entry", TimeSpan.FromSeconds(5));

        [Test]
        public void ALayerWithoutABudgetCannotSpawn() =>
            Assert.That(() => Spawner(0, 0, 0).SpawnAsync(1, null, null, null, CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("spawning is off for this layer"));

        [Test]
        public void TheDepthLimitStopsRecursion() =>
            Assert.That(() => Spawner(3, 3, 10).SpawnAsync(1, null, null, null, CancellationToken.None),
                Throws.InvalidOperationException.With.Message.Contains("depth 3, the layer's maxDepth"));

        [Test]
        public void ASpawnMustFitTheWorkersBudget()
        {
            var spawner = Spawner(0, 3, 2);
            Assert.Multiple(() =>
            {
                Assert.That(() => spawner.SpawnAsync(3, null, null, null, CancellationToken.None),
                    Throws.InvalidOperationException.With.Message.Contains("needs 3 of this worker's budget, which has 2 left"));
                Assert.That(() => spawner.SpawnAsync(2, null, null, 1, CancellationToken.None),
                    Throws.InvalidOperationException.With.Message.Contains("2 children with 1 each need 2, but 0 is left"));
                Assert.That(spawner.Remaining, Is.EqualTo(2), "a refused spawn takes nothing");
            });
        }

        [Test]
        public void InputsNeedOneEntryPerChild() =>
            Assert.That(() => Spawner(0, 3, 5).SpawnAsync(2, null, ["only one"], null, CancellationToken.None),
                Throws.ArgumentException.With.Message.Contains("needs one per spawned point (2)"));
    }
}
