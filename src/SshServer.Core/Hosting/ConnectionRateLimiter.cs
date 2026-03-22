namespace AlfredBr.SshServer.Core;

internal sealed class ConnectionRateLimiter
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _attemptsByClient = new(StringComparer.Ordinal);
    private readonly ISystemClock _clock;

    public ConnectionRateLimiter(int maxAttempts, TimeSpan window, ISystemClock clock)
    {
        if (maxAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts cannot be negative.");
        if (window < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "Window cannot be negative.");

        MaxAttempts = maxAttempts;
        Window = window;
        _clock = clock;
    }

    public int MaxAttempts { get; }

    public TimeSpan Window { get; }

    public bool IsEnabled => MaxAttempts > 0 && Window > TimeSpan.Zero;

    public ConnectionRateLimitDecision Evaluate(string clientKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientKey);

        if (!IsEnabled)
        {
            return new ConnectionRateLimitDecision(true, clientKey, 0, MaxAttempts, Window);
        }

        var now = _clock.UtcNow;
        var cutoff = now - Window;

        lock (_lock)
        {
            foreach (var key in _attemptsByClient.Keys.ToArray())
            {
                var queue = _attemptsByClient[key];
                TrimExpired(queue, cutoff);
                if (queue.Count == 0)
                {
                    _attemptsByClient.Remove(key);
                }
            }

            if (!_attemptsByClient.TryGetValue(clientKey, out var attempts))
            {
                attempts = new Queue<DateTimeOffset>();
                _attemptsByClient[clientKey] = attempts;
            }

            TrimExpired(attempts, cutoff);

            if (attempts.Count >= MaxAttempts)
            {
                return new ConnectionRateLimitDecision(false, clientKey, attempts.Count, MaxAttempts, Window);
            }

            attempts.Enqueue(now);
            return new ConnectionRateLimitDecision(true, clientKey, attempts.Count, MaxAttempts, Window);
        }
    }

    private static void TrimExpired(Queue<DateTimeOffset> attempts, DateTimeOffset cutoff)
    {
        while (attempts.Count > 0 && attempts.Peek() <= cutoff)
        {
            attempts.Dequeue();
        }
    }
}

internal sealed record ConnectionRateLimitDecision(
    bool IsAllowed,
    string ClientKey,
    int AttemptsInWindow,
    int Limit,
    TimeSpan Window);
