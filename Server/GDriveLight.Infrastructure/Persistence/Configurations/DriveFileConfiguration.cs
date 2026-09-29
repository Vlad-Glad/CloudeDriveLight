using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class DriveFileConfiguration : IEntityTypeConfiguration<DriveFileEntity>
{
    public void Configure(EntityTypeBuilder<DriveFileEntity> builder)
    {
        builder.ToTable("DriveFiles");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.FileUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(f => f.ContentHash)
            .IsRequired()
            .HasMaxLength(128);

        // Relationships
        builder.HasOne(f => f.FileType)
            .WithMany()
            .HasForeignKey(f => f.FileTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Folder)
            .WithMany(f => f.Files)
            .HasForeignKey(f => f.FolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.EditedByUser)
            .WithMany()
            .HasForeignKey(f => f.EditedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
