namespace AlfredBr.SshServer.Core.Tests;

public class ConnectionLimiterTests
{
    [Fact]
    public void TryAcquire_AllowsConnectionsUpToConfiguredLimit()
    {
        var limiter = new ConnectionLimiter(maxConnections: 2);

        var firstAccepted = limiter.TryAcquire(out var firstCount);
        var secondAccepted = limiter.TryAcquire(out var secondCount);
        var thirdAccepted = limiter.TryAcquire(out var rejectedCount);

        Assert.True(firstAccepted);
        Assert.True(secondAccepted);
        Assert.False(thirdAccepted);
        Assert.Equal(1, firstCount);
        Assert.Equal(2, secondCount);
        Assert.Equal(2, rejectedCount);
        Assert.Equal(2, limiter.ActiveConnections);
    }

    [Fact]
    public void TryAcquire_AllowsUnlimitedConnectionsWhenLimitIsZero()
    {
        var limiter = new ConnectionLimiter(maxConnections: 0);

        for (var i = 1; i <= 5; i++)
        {
            var accepted = limiter.TryAcquire(out var activeConnections);

            Assert.True(accepted);
            Assert.Equal(i, activeConnections);
        }

        Assert.Equal(5, limiter.ActiveConnections);
    }

    [Fact]
    public void Release_DecrementsCountWithoutGoingNegative()
    {
        var limiter = new ConnectionLimiter(maxConnections: 1);
        limiter.TryAcquire(out _);

        var remainingAfterRelease = limiter.Release();
        var remainingAfterExtraRelease = limiter.Release();

        Assert.Equal(0, remainingAfterRelease);
        Assert.Equal(0, remainingAfterExtraRelease);
        Assert.Equal(0, limiter.ActiveConnections);
    }
}
