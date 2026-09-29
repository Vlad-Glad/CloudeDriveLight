using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class SyncFolderConfiguration : IEntityTypeConfiguration<SyncFolderEntity>
{
    public void Configure(EntityTypeBuilder<SyncFolderEntity> builder)
    {
        builder.ToTable("SyncFolders");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.LocalPath)
            .IsRequired()
            .HasMaxLength(1024);

        // Relationships
        builder.HasOne(s => s.DriveFolder)
            .WithMany()
            .HasForeignKey(s => s.DriveFolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Device)
            .WithMany()
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
