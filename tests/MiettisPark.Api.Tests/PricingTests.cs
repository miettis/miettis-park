using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class PricingTests
{
    public static List<ParkingFeeTier> Tiers() =>
    [
        new() { Amount = 0.50m, PerUnit = TimeSpan.FromMinutes(10), StartDuration = TimeSpan.Zero, EndDuration = TimeSpan.FromHours(3) },
        new() { Amount = 0.30m, PerUnit = TimeSpan.FromMinutes(10), StartDuration = TimeSpan.FromHours(3) }
    ];

    public static IEnumerable<object[]> Prices()
    {
        yield return [0L, 0m];
        yield return [1L, 0.50m];
        yield return [TimeSpan.TicksPerSecond, 0.50m];
        yield return [TimeSpan.TicksPerMinute, 0.50m];
        yield return [TimeSpan.FromMinutes(10).Ticks, 0.50m];
        yield return [TimeSpan.FromMinutes(10).Ticks + 1, 1m];
        yield return [TimeSpan.FromSeconds(10799).Ticks, 9m];
        yield return [TimeSpan.FromHours(3).Ticks, 9m];
        yield return [TimeSpan.FromHours(3).Ticks + 1, 9.30m];
        yield return [TimeSpan.FromMinutes(190).Ticks, 9.30m];
        yield return [TimeSpan.FromMinutes(190).Ticks + 1, 9.60m];
        yield return [TimeSpan.FromHours(4).Ticks, 10.80m];
    }

    [Theory]
    [MemberData(nameof(Prices))]
    public void CalculatesSpecificationBoundaries(long ticks, decimal expected)
    {
        Assert.Equal(expected, new ParkingFeeCalculator().Calculate(TimeSpan.FromTicks(ticks), Tiers()));
    }

    [Fact]
    public void RejectsNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParkingFeeCalculator().Calculate(TimeSpan.FromTicks(-1), Tiers()));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("amount")]
    [InlineData("decimals")]
    [InlineData("unit-zero")]
    [InlineData("unit-negative")]
    [InlineData("first-start")]
    [InlineData("negative-start")]
    [InlineData("end-before-start")]
    [InlineData("end-equals-start")]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("open-middle")]
    [InlineData("closed-last")]
    public void RejectsInvalidConfigurations(string problem)
    {
        var tiers = Tiers();
        switch (problem)
        {
            case "empty":
                tiers.Clear();
                break;
            case "amount":
                tiers[0].Amount = -1m;
                break;
            case "decimals":
                tiers[0].Amount = 0.001m;
                break;
            case "unit-zero":
                tiers[0].PerUnit = TimeSpan.Zero;
                break;
            case "unit-negative":
                tiers[0].PerUnit = TimeSpan.FromTicks(-1);
                break;
            case "first-start":
                tiers[0].StartDuration = TimeSpan.FromTicks(1);
                break;
            case "negative-start":
                tiers[0].StartDuration = TimeSpan.FromTicks(-1);
                break;
            case "end-before-start":
                tiers[0].EndDuration = TimeSpan.FromTicks(-1);
                break;
            case "end-equals-start":
                tiers[0].EndDuration = TimeSpan.Zero;
                break;
            case "gap":
                tiers[1].StartDuration += TimeSpan.FromTicks(1);
                break;
            case "overlap":
                tiers[1].StartDuration -= TimeSpan.FromTicks(1);
                break;
            case "open-middle":
                tiers[0].EndDuration = null;
                break;
            case "closed-last":
                tiers[1].EndDuration = TimeSpan.FromHours(4);
                break;
        }

        Assert.Throws<ArgumentException>(() => new ParkingFeeCalculator().Calculate(TimeSpan.Zero, tiers));
    }

    [Fact]
    public void TickDivisionDoesNotOverflowAtMaximumDuration()
    {
        var tiers = new List<ParkingFeeTier> { new() { Amount = 0.01m, PerUnit = TimeSpan.FromTicks(2) } };
        Assert.Equal(((decimal)(long.MaxValue / 2) + 1) * 0.01m, new ParkingFeeCalculator().Calculate(TimeSpan.MaxValue, tiers));
    }
}
