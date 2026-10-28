using Halyard.Services.Installs;

namespace Halyard.Services.Tests.Installs;

public sealed class CdnBalancerTests
{
    private static readonly Uri[] Cdns = [new("https://a.test/"), new("https://b.test/"), new("https://c.test/")];

    [Fact]
    public void Failover_UsesTheFirstCdnAndRotatesOnRetries()
    {
        var balancer = new CdnBalancer(Cdns, spread: false);

        Assert.Equal(0, balancer.Pick(attempt: 1));
        Assert.Equal(1, balancer.Pick(attempt: 2));
        Assert.Equal(2, balancer.Pick(attempt: 3));
        Assert.Equal(1, balancer.Pick(attempt: 1, exclude: 0));
    }

    [Fact]
    public void Spread_MeasuresEveryCdnThenFavoursTheFastest()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);

        var explored = Enumerable.Range(0, 3).Select(_ => balancer.Pick(1)).ToHashSet();
        Assert.Equal([0, 1, 2], explored.Order());

        balancer.Report(0, 1_000_000, TimeSpan.FromSeconds(1), success: true);
        balancer.Report(1, 1_000_000, TimeSpan.FromSeconds(10), success: true);
        balancer.Report(2, 0, TimeSpan.FromSeconds(1), success: false);

        var picks = Enumerable.Range(0, 1000).Select(_ => balancer.Pick(1)).ToList();
        Assert.True(picks.Count(pick => pick == 0) > 800);
    }

    [Fact]
    public void Spread_KeepsSlowCdnsOffTheWorkersOnceMeasured()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);
        for (var i = 0; i < 3; i++)
        {
            balancer.Pick(1);
        }

        // B is fast, A and C a quarter of its speed: they would hold most workers if picked by weight.
        balancer.Report(0, 1_000_000, TimeSpan.FromSeconds(4), success: true);
        balancer.Report(1, 1_000_000, TimeSpan.FromSeconds(1), success: true);
        balancer.Report(2, 1_000_000, TimeSpan.FromSeconds(4), success: true);

        Assert.All(Enumerable.Range(0, 64), _ => Assert.Equal(1, balancer.Pick(1)));
    }

    [Fact]
    public void Spread_SharesTheLoadBetweenComparableCdns()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);
        for (var i = 0; i < 3; i++)
        {
            balancer.Pick(1);
        }

        balancer.Report(0, 1_000_000, TimeSpan.FromSeconds(1), success: true);
        balancer.Report(1, 1_000_000, TimeSpan.FromSeconds(1.1), success: true);
        balancer.Report(2, 1_000_000, TimeSpan.FromSeconds(10), success: true);

        var picks = Enumerable.Range(0, 64).Select(_ => balancer.Pick(1)).ToList();
        Assert.DoesNotContain(2, picks);
        Assert.InRange(picks.Count(pick => pick == 0), 28, 40);
    }

    [Fact]
    public void Spread_KeepsAWorkerOnItsCdnWhileItIsCompetitive()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);
        for (var i = 0; i < 3; i++)
        {
            balancer.Pick(1);
        }

        balancer.Report(0, 1_000_000, TimeSpan.FromSeconds(1), success: true);
        balancer.Report(1, 1_000_000, TimeSpan.FromSeconds(1.1), success: true);
        balancer.Report(2, 1_000_000, TimeSpan.FromSeconds(10), success: true);

        // B is less busy than A after these, but a worker already on A stays there (warm connection).
        for (var i = 0; i < 8; i++)
        {
            balancer.Pick(1, preferred: 0);
        }

        Assert.Equal(0, balancer.Pick(1, preferred: 0));

        // C isn't competitive: a worker preferring it moves.
        Assert.NotEqual(2, balancer.Pick(1, preferred: 2));
    }

    [Fact]
    public void Spread_AbandonedSlowRequestsLowerTheCdnSpeed()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);
        for (var i = 0; i < 3; i++)
        {
            balancer.Pick(1);
        }

        balancer.Report(0, 1_000_000, TimeSpan.FromSeconds(0.1), success: true);
        balancer.Report(1, 1_000_000, TimeSpan.FromSeconds(0.2), success: true);
        balancer.Report(2, 1_000_000, TimeSpan.FromSeconds(0.2), success: true);

        // A slows to a crawl: its requests only ever lose hedge races.
        for (var i = 0; i < 20; i++)
        {
            balancer.Abandon(0, 1_000, TimeSpan.FromSeconds(2));
        }

        Assert.DoesNotContain(0, Enumerable.Range(0, 32).Select(_ => balancer.Pick(1)));
    }

    [Fact]
    public void Spread_NeverPicksTheExcludedCdn()
    {
        var balancer = new CdnBalancer(Cdns, spread: true);

        Assert.All(Enumerable.Range(0, 200), _ => Assert.NotEqual(1, balancer.Pick(1, exclude: 1)));
    }
}
