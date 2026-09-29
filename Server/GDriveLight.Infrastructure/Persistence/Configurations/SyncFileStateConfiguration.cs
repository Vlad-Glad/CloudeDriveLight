using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class SyncFileStateConfiguration : IEntityTypeConfiguration<SyncFileStateEntity>
{
    public void Configure(EntityTypeBuilder<SyncFileStateEntity> builder)
    {
        builder.ToTable("SyncFileStates");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RelativePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(s => s.LocalHash)
            .HasMaxLength(128);

        builder.Property(s => s.LastSyncedHash)
            .HasMaxLength(128);

        // Relationships
        builder.HasOne(s => s.SyncFolder)
            .WithMany()
            .HasForeignKey(s => s.SyncFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.DriveFile)
            .WithMany()
            .HasForeignKey(s => s.DriveFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
