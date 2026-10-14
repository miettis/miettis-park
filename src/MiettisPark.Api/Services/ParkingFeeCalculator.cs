using MiettisPark.Api.Data;

namespace MiettisPark.Api.Services;

public sealed class ParkingFeeCalculator
{
    public decimal Calculate(TimeSpan duration, IReadOnlyList<ParkingFeeTier> tiers)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Validate(tiers);
        decimal cost = 0;
        foreach (var tier in tiers)
        {
            var end = tier.EndDuration is { } limit && limit < duration ? limit : duration;
            if (end <= tier.StartDuration)
            {
                continue;
            }

            var ticks = end.Ticks - tier.StartDuration.Ticks;
            var units = ticks / tier.PerUnit.Ticks;
            if (ticks % tier.PerUnit.Ticks != 0)
            {
                units++;
            }

            cost += units * tier.Amount;
        }

        return cost;
    }

    public void Validate(IReadOnlyList<ParkingFeeTier> tiers)
    {
        if (tiers.Count == 0)
        {
            throw new ArgumentException("Parking fee configuration must not be empty.", nameof(tiers));
        }

        var expectedStart = TimeSpan.Zero;
        for (var index = 0; index < tiers.Count; index++)
        {
            var tier = tiers[index];
            if (tier.Amount < 0 || decimal.Round(tier.Amount, 2) != tier.Amount
                || tier.PerUnit <= TimeSpan.Zero || tier.StartDuration != expectedStart
                || (index == tiers.Count - 1 ? tier.EndDuration != null : tier.EndDuration == null)
                || tier.EndDuration is { } end && end <= tier.StartDuration)
            {
                throw new ArgumentException("Invalid parking fee configuration.", nameof(tiers));
            }

            expectedStart = tier.EndDuration ?? tier.StartDuration;
        }
    }
}
