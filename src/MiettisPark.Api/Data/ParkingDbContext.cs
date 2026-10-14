using Microsoft.EntityFrameworkCore;

namespace MiettisPark.Api.Data;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    public DbSet<ParkingGarage> ParkingGarages => Set<ParkingGarage>();
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Parking> Parkings => Set<Parking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParkingDbContext).Assembly);
    }
}
