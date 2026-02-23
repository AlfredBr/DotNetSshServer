using System.Collections.Concurrent;

using Spectre.Console;

namespace SshServer.Tui;

/// <summary>
/// Implements IAnsiConsoleInput to provide keyboard input to Spectre.Console
/// from SSH channel data.
/// </summary>
public class SshAnsiConsoleInput : IAnsiConsoleInput
{
    private readonly BlockingCollection<ConsoleKeyInfo> _keyQueue = new();
    private readonly EscapeSequenceParser _parser = new();
    private readonly object _lock = new();

    /// <summary>
    /// Feed raw bytes from the SSH channel. Parsed keys are queued for ReadKey.
    /// </summary>
    public void EnqueueData(byte[] data)
    {
        lock (_lock)
        {
            foreach (var b in data)
            {
                var key = _parser.Feed(b);
                if (key.HasValue)
                {
                    _keyQueue.Add(key.Value);
                }
            }
        }
    }

    /// <summary>
    /// Flush any pending escape sequence (e.g., lone Escape key).
    /// Call this on timeout to ensure Escape key is delivered.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            var key = _parser.Reset();
            if (key.HasValue)
            {
                _keyQueue.Add(key.Value);
            }
        }
    }

    /// <summary>
    /// Check if a key is available without blocking.
    /// </summary>
    public bool IsKeyAvailable()
    {
        return _keyQueue.Count > 0;
    }

    /// <summary>
    /// Read the next key, blocking if necessary.
    /// </summary>
    /// <param name="intercept">If true, don't echo (ignored, we never echo).</param>
    /// <returns>The next ConsoleKeyInfo, or null if cancelled.</returns>
    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        try
        {
            // Block until a key is available
            return _keyQueue.Take();
        }
        catch (InvalidOperationException)
        {
            // Collection was completed
            return null;
        }
    }

    /// <summary>
    /// Read the next key asynchronously.
    /// </summary>
    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _keyQueue.Take(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return (ConsoleKeyInfo?)null;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Signal that no more input will be provided.
    /// </summary>
    public void Complete()
    {
        _keyQueue.CompleteAdding();
    }

    /// <summary>
    /// Clear any pending keys in the queue.
    /// </summary>
    public void Clear()
    {
        while (_keyQueue.TryTake(out _)) { }
    }
}
