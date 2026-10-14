using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiettisPark.Api.Data.Configurations;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits", table =>
        {
            table.HasCheckConstraint("ck_visits_license", "length(license_number) BETWEEN 1 AND 20");
            table.HasCheckConstraint("ck_visits_times", "(end_at IS NULL OR end_at >= start_at) AND (closed_at IS NULL OR closed_at >= start_at)");
            table.HasCheckConstraint("ck_visits_cost", "cost IS NULL OR cost >= 0");
            table.HasCheckConstraint("ck_visits_lifecycle", """
                (status = 'Active' AND end_at IS NULL AND closed_at IS NULL AND cost IS NULL AND closure_reason IS NULL)
                OR (status = 'Completed' AND end_at IS NOT NULL AND closed_at IS NOT NULL AND closed_at = end_at AND cost IS NOT NULL)
                OR (status = 'Interrupted' AND end_at IS NULL AND closed_at IS NOT NULL AND cost IS NULL AND closure_reason IS NOT NULL)
                """);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ParkingGarageId).HasColumnName("parking_garage_id");
        builder.Property(x => x.LicenseNumber).HasColumnName("license_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.StartAt).HasColumnName("start_at");
        builder.Property(x => x.EndAt).HasColumnName("end_at");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ClosureReason).HasColumnName("closure_reason").HasMaxLength(100);
        builder.Property(x => x.Cost).HasColumnName("cost").HasPrecision(12, 2);
        builder.HasOne(x => x.ParkingGarage).WithMany(x => x.Visits)
            .HasForeignKey(x => x.ParkingGarageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ParkingGarageId, x.LicenseNumber })
            .IsUnique().HasFilter("status = 'Active'");
    }
}
