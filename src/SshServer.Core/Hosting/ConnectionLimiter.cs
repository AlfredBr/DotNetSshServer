using System.Threading;

namespace AlfredBr.SshServer.Core;

internal sealed class ConnectionLimiter
{
    private int _activeConnections;

    public ConnectionLimiter(int maxConnections)
    {
        if (maxConnections < 0)
            throw new ArgumentOutOfRangeException(nameof(maxConnections), "Max connections cannot be negative.");

        MaxConnections = maxConnections;
    }

    public int MaxConnections { get; }

    public int ActiveConnections => Volatile.Read(ref _activeConnections);

    public bool TryAcquire(out int activeConnections)
    {
        while (true)
        {
            var current = Volatile.Read(ref _activeConnections);
            if (MaxConnections > 0 && current >= MaxConnections)
            {
                activeConnections = current;
                return false;
            }

            var updated = current + 1;
            if (Interlocked.CompareExchange(ref _activeConnections, updated, current) == current)
            {
                activeConnections = updated;
                return true;
            }
        }
    }

    public int Release()
    {
        while (true)
        {
            var current = Volatile.Read(ref _activeConnections);
            if (current == 0)
                return 0;

            var updated = current - 1;
            if (Interlocked.CompareExchange(ref _activeConnections, updated, current) == current)
            {
                return updated;
            }
        }
    }
}
