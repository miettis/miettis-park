using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiettisPark.Api.Data;

/// <summary>Constructs the model for migrations without starting the HTTP host or connecting to PostgreSQL.</summary>
public sealed class ParkingDbContextFactory : IDesignTimeDbContextFactory<ParkingDbContext>
{
    public const string DesignTimeConnectionString =
        "Host=127.0.0.1;Port=1;Database=miettis_park_design;Username=design;Password=unused;Timeout=1";

    public ParkingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Parking")
            ?? DesignTimeConnectionString;
        var options = new DbContextOptionsBuilder<ParkingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ParkingDbContext(options);
    }
}
