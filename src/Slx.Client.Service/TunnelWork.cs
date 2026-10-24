namespace Slx.Client.Service;

/// <summary>Allows an explicit off/shutdown to interrupt network readiness while lifecycle mutations remain serialized.</summary>
internal sealed class TunnelWork
{
    private readonly object sync = new();
    private CancellationTokenSource? active;
    private int stopRequests;

    public Work Begin(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (stopRequests != 0) source.Cancel();
            active = source;
            return new(this, source);
        }
    }

    public IDisposable RequestStop()
    {
        CancellationTokenSource? interrupted;
        lock (sync) { stopRequests++; interrupted = active; }
        try { interrupted?.Cancel(); }
        catch (ObjectDisposedException) { }
        return new StopRequest(this);
    }

    internal sealed class Work(TunnelWork owner, CancellationTokenSource source) : IDisposable
    {
        public CancellationToken Token => source.Token;
        public void Dispose()
        {
            lock (owner.sync) { if (ReferenceEquals(owner.active, source)) owner.active = null; }
            source.Dispose();
        }
    }

    private sealed class StopRequest(TunnelWork owner) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.sync) owner.stopRequests--;
        }
    }
}
