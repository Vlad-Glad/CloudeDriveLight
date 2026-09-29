using Microsoft.EntityFrameworkCore;
using GDriveLight.

namespace GDriveLight.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }


        public DbSet<DriveFile> Files { get; set; }

    }
}
