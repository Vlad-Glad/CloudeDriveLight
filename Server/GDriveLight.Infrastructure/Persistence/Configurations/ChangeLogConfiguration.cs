using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class ChangeLogConfiguration : IEntityTypeConfiguration<ChangeLogEntity>
{
    public void Configure(EntityTypeBuilder<ChangeLogEntity> builder)
    {
        builder.ToTable("ChangeLogs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(c => c.ResourceId)
            .IsRequired();

        builder.Property(c => c.ResourceType)
            .IsRequired();

        builder.Property(c => c.EventType)
            .IsRequired();

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(255);

        builder.Property(c => c.ContentHash)
            .HasMaxLength(128);

        builder.Property(c => c.TimestampUtc)
            .IsRequired();

        // Foreign keys
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.OriginDevice)
            .WithMany()
            .HasForeignKey(c => c.OriginDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Crucial composite index: WHERE UserId = @id AND Id > @cursor
        builder.HasIndex(c => new { c.UserId, c.Id });

        // Index on ResourceId for lookup by resource
        builder.HasIndex(c => c.ResourceId);
    }
}
