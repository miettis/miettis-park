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
        builder.ToTable("parking_garages", table =>
        {
            table.HasCheckConstraint("ck_garages_layout", "layout_width BETWEEN 1 AND 50 AND layout_height BETWEEN 1 AND 50 AND layout_version > 0");
            table.HasCheckConstraint("ck_garages_location", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(300);
        builder.Property(x => x.Latitude).HasColumnName("latitude");
        builder.Property(x => x.Longitude).HasColumnName("longitude");
        builder.Property(x => x.LayoutWidth).HasColumnName("layout_width");
        builder.Property(x => x.LayoutHeight).HasColumnName("layout_height");
        builder.Property(x => x.LayoutVersion).HasColumnName("layout_version").IsConcurrencyToken();
        var cells = builder.Property(x => x.LayoutCells).HasColumnName("layout_cells").HasColumnType("jsonb")
            .HasConversion(value => SerializeCells(value), value => DeserializeCells(value));
        cells.Metadata.SetValueComparer(new ValueComparer<List<LayoutCell>>(
            (left, right) => SerializeCells(left!) == SerializeCells(right!),
            value => SerializeCells(value).GetHashCode(),
            value => DeserializeCells(SerializeCells(value))));

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
            LayoutWidth = 5,
            LayoutCells = Enumerable.Range(1, 5).Select(id => new LayoutCell(id - 1, 0, LayoutCellType.ParkingLot, id)).ToList(),
            Name = "Keskustan pysäköinti",
            ParkingFeeConfiguration = DefaultParkingFees.Create()
        });
    }

    private static string SerializeCells(List<LayoutCell> cells) => JsonSerializer.Serialize(cells, JsonOptions);
    private static List<LayoutCell> DeserializeCells(string value) => JsonSerializer.Deserialize<List<LayoutCell>>(value, JsonOptions)
        ?? throw new JsonException("Layout must be an array.");

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
