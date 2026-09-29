using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class DriveFolderConfiguration : IEntityTypeConfiguration<DriveFolderEntity>
{
    public void Configure(EntityTypeBuilder<DriveFolderEntity> builder)
    {
        builder.ToTable("DriveFolders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(255);

        // Relationships
        builder.HasOne(f => f.ParentFolder)
            .WithMany(f => f.SubFolders)
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
