using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiettisPark.Api.Data.Configurations;

public sealed class ParkingGarageConfiguration : IEntityTypeConfiguration<ParkingGarage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<ParkingGarage> builder)
    {
        builder.ToTable("parking_garages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        // The contract calls for a root JSON array. Explicit serialization preserves that shape.
        // Structural comparison also detects edits made to a tier inside the mutable list.
        var comparer = new ValueComparer<List<ParkingFeeTier>>(
            (left, right) => Serialize(left!) == Serialize(right!),
            value => Serialize(value).GetHashCode(),
            value => Deserialize(Serialize(value)));

        var tiers = builder.Property(x => x.ParkingFeeConfiguration)
            .HasColumnName("parking_fee_configuration")
            .HasColumnType("jsonb")
            .HasConversion(value => Serialize(value), value => Deserialize(value))
            .IsRequired();
        tiers.Metadata.SetValueComparer(comparer);

        builder.HasData(new ParkingGarage
        {
            Id = 1,
            Name = "Keskustan pysäköinti",
            ParkingFeeConfiguration =
            [
                new ParkingFeeTier
                {
                    Amount = 0.50m,
                    PerUnit = TimeSpan.FromMinutes(10),
                    StartDuration = TimeSpan.Zero,
                    EndDuration = TimeSpan.FromHours(3)
                },
                new ParkingFeeTier
                {
                    Amount = 0.30m,
                    PerUnit = TimeSpan.FromMinutes(10),
                    StartDuration = TimeSpan.FromHours(3),
                    EndDuration = null
                }
            ]
        });
    }

    private static string Serialize(List<ParkingFeeTier> value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static List<ParkingFeeTier> Deserialize(string value)
    {
        return JsonSerializer.Deserialize<List<ParkingFeeTier>>(value, JsonOptions)
            ?? throw new JsonException("Parking fee configuration must be a JSON array.");
    }
}
