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

        builder.HasData(
            // Documents & text
            new FileTypeEntity { Id = 1,  Name = "HTML",       MimeType = "text/html",         Extension = ".html" },
            new FileTypeEntity { Id = 2,  Name = "Text",       MimeType = "text/plain",         Extension = ".txt"  },
            new FileTypeEntity { Id = 3,  Name = "XML",        MimeType = "text/xml",           Extension = ".xml"  },
            // Source code
            new FileTypeEntity { Id = 4,  Name = "C",          MimeType = "text/x-c",           Extension = ".c"    },
            new FileTypeEntity { Id = 5,  Name = "C++",        MimeType = "text/x-c++",         Extension = ".cpp"  },
            new FileTypeEntity { Id = 6,  Name = "C#",         MimeType = "text/x-csharp",      Extension = ".cs"   },
            new FileTypeEntity { Id = 7,  Name = "Java",       MimeType = "text/x-java",        Extension = ".java" },
            new FileTypeEntity { Id = 8,  Name = "JavaScript", MimeType = "text/javascript",    Extension = ".js"   },
            new FileTypeEntity { Id = 9,  Name = "Kotlin",     MimeType = "text/x-kotlin",      Extension = ".kt"   },
            new FileTypeEntity { Id = 10, Name = "Python",     MimeType = "text/x-python",      Extension = ".py"   },
            // Images
            new FileTypeEntity { Id = 11, Name = "JPEG",       MimeType = "image/jpeg",         Extension = ".jpg"  },
            new FileTypeEntity { Id = 12, Name = "PNG",        MimeType = "image/png",          Extension = ".png"  }
        );
    }
}
