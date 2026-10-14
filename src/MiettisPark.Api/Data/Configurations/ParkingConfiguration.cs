using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiettisPark.Api.Data.Configurations;

public sealed class ParkingConfiguration : IEntityTypeConfiguration<Parking>
{
    public void Configure(EntityTypeBuilder<Parking> builder)
    {
        builder.ToTable("parkings", table =>
        {
            table.HasCheckConstraint("ck_parkings_times", "(end_at IS NULL OR end_at >= start_at) AND (closed_at IS NULL OR closed_at >= start_at)");
            table.HasCheckConstraint("ck_parkings_lifecycle", """
                (status = 'Active' AND end_at IS NULL AND closed_at IS NULL)
                OR (status = 'Completed' AND end_at IS NOT NULL AND closed_at IS NOT NULL AND closed_at = end_at)
                OR (status = 'Interrupted' AND end_at IS NULL AND closed_at IS NOT NULL)
                """);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.VisitId).HasColumnName("visit_id").IsRequired(false);
        builder.Property(x => x.ParkingLotId).HasColumnName("parking_lot_id");
        builder.Property(x => x.StartAt).HasColumnName("start_at");
        builder.Property(x => x.EndAt).HasColumnName("end_at");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Visit).WithMany(x => x.Parkings)
            .HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParkingLot).WithMany(x => x.Parkings)
            .HasForeignKey(x => x.ParkingLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ParkingLotId).IsUnique().HasFilter("status = 'Active'");
        builder.HasIndex(x => x.VisitId).IsUnique().HasFilter("status = 'Active' AND visit_id IS NOT NULL");
    }
}
