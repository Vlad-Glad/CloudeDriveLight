using GDriveLight.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GDriveLight.Infrastructure.Persistence.Configurations;

public class FileTypeConfiguration : IEntityTypeConfiguration<FileTypeEntity>
{
    public void Configure(EntityTypeBuilder<FileTypeEntity> builder)
    {
        builder.ToTable("FileTypes");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Extension)
            .IsRequired()
            .HasMaxLength(20);
    }
}
