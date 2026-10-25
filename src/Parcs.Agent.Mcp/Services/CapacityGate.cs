namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// Admission control for layers: a layer holds <c>parallelism</c> worker slots while it runs, and
/// layers that do not fit wait in strict FIFO order, so a wide layer is not starved by narrow ones.
/// The capacity is the cluster's maxParallelism and may change between requests.
/// </summary>
public sealed class CapacityGate
{
    private readonly Lock _lock = new();
    private readonly LinkedList<Waiter> _queue = new();
    private int _inUse;
    private int _capacity;

    public CapacityGate(int initialCapacity = 0) => _capacity = initialCapacity;

    public int InUse { get { lock (_lock) return _inUse; } }

    public int Queued { get { lock (_lock) return _queue.Count; } }

    public int Capacity { get { lock (_lock) return _capacity; } }

    /// <summary>Updates the capacity and admits waiters that now fit.</summary>
    public void SetCapacity(int capacity)
    {
        lock (_lock)
        {
            _capacity = Math.Max(0, capacity);
            Pump();
        }
    }

    /// <summary>
    /// Waits until <paramref name="slots"/> slots are free, then returns a lease that frees them
    /// when disposed. The caller checks <paramref name="slots"/> against the capacity first.
    /// </summary>
    public Task<IDisposable> AcquireAsync(int slots, CancellationToken ct)
    {
        Waiter waiter;
        lock (_lock)
        {
            if (_queue.Count == 0 && _inUse + slots <= _capacity)
            {
                _inUse += slots;
                return Task.FromResult<IDisposable>(new Lease(this, slots));
            }

            waiter = new Waiter(slots);
            waiter.Node = _queue.AddLast(waiter);
        }

        if (ct.CanBeCanceled)
        {
            waiter.Registration = ct.Register(() =>
            {
                lock (_lock)
                {
                    if (waiter.Node?.List is null)
                        return;   // already admitted
                    _queue.Remove(waiter.Node);
                    Pump();       // the head may have changed
                }
                waiter.Completion.TrySetCanceled(ct);
            });
        }

        return waiter.Completion.Task;
    }

    private void Release(int slots)
    {
        lock (_lock)
        {
            _inUse -= slots;
            Pump();
        }
    }

    // Admits waiters from the head of the queue while they fit. Called under _lock.
    private void Pump()
    {
        while (_queue.First is { } head && _inUse + head.Value.Slots <= _capacity)
        {
            _queue.RemoveFirst();
            var waiter = head.Value;
            _inUse += waiter.Slots;
            // Unregister, not Dispose: Dispose blocks on a running callback, which needs _lock.
            waiter.Registration.Unregister();
            if (!waiter.Completion.TrySetResult(new Lease(this, waiter.Slots)))
                _inUse -= waiter.Slots;   // cancelled concurrently
        }
    }

    private sealed class Waiter(int slots)
    {
        public int Slots { get; } = slots;
        public LinkedListNode<Waiter>? Node { get; set; }
        public CancellationTokenRegistration Registration { get; set; }
        public TaskCompletionSource<IDisposable> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Lease(CapacityGate gate, int slots) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gate.Release(slots);
        }
    }
}
