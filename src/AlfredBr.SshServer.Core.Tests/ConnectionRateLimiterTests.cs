namespace AlfredBr.SshServer.Core.Tests;

public class ConnectionRateLimiterTests
{
    [Fact]
    public void Evaluate_AllowsAttemptsUpToConfiguredThreshold()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero));
        var limiter = new ConnectionRateLimiter(2, TimeSpan.FromSeconds(30), clock);

        var first = limiter.Evaluate("127.0.0.1");
        var second = limiter.Evaluate("127.0.0.1");
        var third = limiter.Evaluate("127.0.0.1");

        Assert.True(first.IsAllowed);
        Assert.Equal(1, first.AttemptsInWindow);
        Assert.True(second.IsAllowed);
        Assert.Equal(2, second.AttemptsInWindow);
        Assert.False(third.IsAllowed);
        Assert.Equal(2, third.AttemptsInWindow);
    }

    [Fact]
    public void Evaluate_AllowsAttemptsAgainAfterWindowExpires()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero));
        var limiter = new ConnectionRateLimiter(1, TimeSpan.FromSeconds(30), clock);

        Assert.True(limiter.Evaluate("127.0.0.1").IsAllowed);
        Assert.False(limiter.Evaluate("127.0.0.1").IsAllowed);

        clock.Advance(TimeSpan.FromSeconds(31));

        var next = limiter.Evaluate("127.0.0.1");
        Assert.True(next.IsAllowed);
        Assert.Equal(1, next.AttemptsInWindow);
    }

    [Fact]
    public void Evaluate_TracksClientsIndependently()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero));
        var limiter = new ConnectionRateLimiter(1, TimeSpan.FromSeconds(30), clock);

        Assert.True(limiter.Evaluate("127.0.0.1").IsAllowed);
        Assert.True(limiter.Evaluate("127.0.0.2").IsAllowed);
        Assert.False(limiter.Evaluate("127.0.0.1").IsAllowed);
    }

    [Fact]
    public void Evaluate_AllowsAllAttemptsWhenDisabled()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero));
        var limiter = new ConnectionRateLimiter(0, TimeSpan.Zero, clock);

        for (var i = 0; i < 5; i++)
        {
            var decision = limiter.Evaluate("127.0.0.1");
            Assert.True(decision.IsAllowed);
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }
}
