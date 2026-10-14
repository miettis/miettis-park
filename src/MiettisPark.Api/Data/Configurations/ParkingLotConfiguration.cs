using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiettisPark.Api.Data.Configurations;

public sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        builder.ToTable("parking_lots", table =>
        {
            table.HasCheckConstraint("ck_parking_lots_number", "number > 0");
            table.HasCheckConstraint("ck_parking_lots_status", "current_status IN ('Free', 'Occupied', 'Unknown')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ParkingGarageId).HasColumnName("parking_garage_id");
        builder.Property(x => x.Number).HasColumnName("number");
        builder.Property(x => x.CurrentStatus).HasColumnName("current_status")
            .HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.ParkingGarageId, x.Number }).IsUnique();
        builder.HasOne(x => x.ParkingGarage).WithMany(x => x.Lots)
            .HasForeignKey(x => x.ParkingGarageId).OnDelete(DeleteBehavior.Restrict);

        for (var number = 1; number <= 5; number++)
        {
            builder.HasData(new ParkingLot
            {
                Id = number,
                ParkingGarageId = 1,
                Number = number,
                CurrentStatus = ParkingLotStatus.Free
            });
        }
    }
}
