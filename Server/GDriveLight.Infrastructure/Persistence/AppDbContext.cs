using GDriveLight.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUserEntity, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<DeviceEntity> Devices { get; set; }
        public DbSet<DriveFileEntity> DriveFiles { get; set; }
        public DbSet<DriveFolderEntity> DriveFolders { get; set; }
        public DbSet<FileTypeEntity> FileTypes { get; set; }
        public DbSet<SyncFolderEntity> SyncFolders { get; set; }
        public DbSet<SyncFileStateEntity> SyncFileStates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
