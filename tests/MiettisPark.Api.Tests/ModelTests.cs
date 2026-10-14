using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MiettisPark.Api.Data;
using Xunit;

namespace MiettisPark.Api.Tests;

public sealed class ModelTests
{
    [Fact]
    public void NpgsqlModelAndMigrationPreserveOfflineDatabaseContract()
    {
        using var database = new ParkingDbContext(new DbContextOptionsBuilder<ParkingDbContext>()
            .UseNpgsql(ParkingDbContextFactory.DesignTimeConnectionString).Options);
        var model = database.GetService<IDesignTimeModel>().Model;
        var visit = model.FindEntityType(typeof(Visit))!;
        var parking = model.FindEntityType(typeof(Parking))!;
        var lot = model.FindEntityType(typeof(ParkingLot))!;
        var garage = model.FindEntityType(typeof(ParkingGarage))!;
        Assert.True(parking.FindProperty(nameof(Parking.VisitId))!.IsNullable);
        Assert.Equal(typeof(string), visit.FindProperty(nameof(Visit.Status))!.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal("Active", visit.FindProperty(nameof(Visit.Status))!.GetTypeMapping().Converter!.ConvertToProvider(VisitStatus.Active));
        Assert.Equal("Active", parking.FindProperty(nameof(Parking.Status))!.GetTypeMapping().Converter!.ConvertToProvider(ParkingStatus.Active));
        Assert.Contains(visit.GetIndexes(), i => i.IsUnique && i.GetFilter() == "status = 'Active'");
        Assert.Contains(parking.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(Parking.ParkingLotId) && i.GetFilter() == "status = 'Active'");
        Assert.Contains(parking.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(Parking.VisitId) && i.GetFilter() == "status = 'Active' AND visit_id IS NOT NULL");
        Assert.Contains(lot.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ParkingLot.ParkingGarageId), nameof(ParkingLot.Number) }));
        Assert.Single(visit.GetForeignKeys());
        Assert.Equal(2, parking.GetForeignKeys().Count());
        Assert.All(visit.GetForeignKeys().Concat(parking.GetForeignKeys()).Concat(lot.GetForeignKeys()), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(visit.GetCheckConstraints(), c => c.Name == "ck_visits_lifecycle" && c.Sql.Contains("cost IS NULL") && c.Sql.Contains("closed_at = end_at"));
        Assert.Contains(parking.GetCheckConstraints(), c => c.Name == "ck_parkings_lifecycle" && c.Sql.Contains("closed_at = end_at"));
        Assert.Contains(visit.GetCheckConstraints(), c => c.Name == "ck_visits_times");
        Assert.Contains(visit.GetCheckConstraints(), c => c.Name == "ck_visits_cost");
        Assert.Contains(lot.GetCheckConstraints(), c => c.Name == "ck_parking_lots_status");
        Assert.Equal("jsonb", garage.FindProperty(nameof(ParkingGarage.ParkingFeeConfiguration))!.GetColumnType());
        Assert.Equal(5, lot.GetSeedData().Count());
        Assert.Single(garage.GetSeedData());
        var property = garage.FindProperty(nameof(ParkingGarage.ParkingFeeConfiguration))!;
        var tiers = PricingTests.Tiers();
        var serialized = (string)property.GetTypeMapping().Converter!.ConvertToProvider(tiers)!;
        using var json = JsonDocument.Parse(serialized);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal("00:10:00", json.RootElement[0].GetProperty("perUnit").GetString());
        var roundTrip = (List<ParkingFeeTier>)property.GetTypeMapping().Converter!.ConvertFromProvider(serialized)!;
        Assert.Equal(tiers[0].PerUnit, roundTrip[0].PerUnit);
        var snapshot = property.GetValueComparer()!.Snapshot(tiers);
        tiers[0].Amount = 1m;
        Assert.False(property.GetValueComparer()!.Equals(tiers, snapshot));
        Assert.True(garage.FindProperty(nameof(ParkingGarage.LayoutVersion))!.IsConcurrencyToken);
        Assert.Equal("jsonb", garage.FindProperty(nameof(ParkingGarage.LayoutCells))!.GetColumnType());
        Assert.Contains(lot.GetIndexes(), index => index.IsUnique && index.GetFilter() == "is_active = true");
        var layoutProperty = garage.FindProperty(nameof(ParkingGarage.LayoutCells))!;
        var cells = new List<LayoutCell> { new(0, 0, LayoutCellType.ParkingLot, 99) };
        var cellSnapshot = layoutProperty.GetValueComparer()!.Snapshot(cells);
        cells[0] = cells[0] with { LotId = 100 };
        Assert.False(layoutProperty.GetValueComparer()!.Equals(cells, cellSnapshot));
        Assert.False(database.Database.HasPendingModelChanges());
        var sql = database.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_visits_parking_garage_id_license_number\"", sql);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_parkings_parking_lot_id\"", sql);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_parkings_visit_id\"", sql);
        Assert.Contains("WHERE status = 'Active' AND visit_id IS NOT NULL", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
        Assert.Contains("visit_id integer", sql);
        Assert.DoesNotContain("visit_id integer NOT NULL", sql);
        Assert.Contains("status = 'Interrupted'", sql);
        Assert.Contains("end_at IS NULL", sql);
        Assert.Contains("cost IS NULL", sql);
        Assert.Contains("ck_visits_lifecycle", sql);
        Assert.Contains("ck_parkings_lifecycle", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("numeric(12,2)", sql);
        var layoutSql = database.GetService<IMigrator>().GenerateScript("20261014125724_InitialCreate");
        Assert.Contains("HAVING count(*) > 2500", layoutSql);
        Assert.Contains("RAISE EXCEPTION", layoutSql);
        Assert.Contains("ORDER BY number, id", layoutSql);
        Assert.Contains("greatest(1, least(n, 50))", layoutSql);
        Assert.Contains("greatest(1, (n + 49) / 50)", layoutSql);
        Assert.Contains("generate_series(0, d.width * d.height - 1)", layoutSql);
        Assert.Contains("'x', pos % d.width, 'y', pos / d.width", layoutSql);
        Assert.Contains("WHERE is_active = true", layoutSql);
        Assert.DoesNotContain("DELETE FROM", layoutSql);
        Assert.DoesNotContain("UPDATE parking_lots", layoutSql);
        Assert.DoesNotContain("UPDATE visits", layoutSql);
        Assert.DoesNotContain("UPDATE parkings", layoutSql);
        Assert.DoesNotContain("parking_fee_configuration =", layoutSql);
        Assert.DoesNotContain("WHERE id = 1", layoutSql);
        Assert.Contains("ck_garages_location", layoutSql);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(5, 5, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(2500, 50, 50)]
    public void OfflineBackfillArithmeticMatchesSqlBoundaries(int count, int expectedWidth, int expectedHeight)
    {
        // Reference calculation plus exact SQL assertions above, not a PostgreSQL migration run.
        var width = Math.Max(1, Math.Min(count, 50));
        var height = Math.Max(1, (count + 49) / 50);
        Assert.Equal((expectedWidth, expectedHeight), (width, height));
        var cells = Enumerable.Range(0, width * height).Select(position => new
        {
            X = position % width,
            Y = position / width,
            HasLot = position < count
        }).ToList();
        Assert.Equal(count, cells.Count(cell => cell.HasLot));
        Assert.Equal(cells.Count, cells.Select(cell => (cell.X, cell.Y)).Distinct().Count());
        Assert.All(cells, cell => Assert.InRange(cell.Y, 0, height - 1));
    }

}
