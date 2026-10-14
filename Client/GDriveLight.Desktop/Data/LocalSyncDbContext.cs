using GDriveLight.Desktop.Models.Sync;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Data;

public class LocalSyncDbContext : DbContext
{
    public DbSet<LocalItemState> Items => Set<LocalItemState>();
    public DbSet<SyncScope> Scopes => Set<SyncScope>();
    public DbSet<SyncCursor> Cursors => Set<SyncCursor>();

    public LocalSyncDbContext(DbContextOptions<LocalSyncDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalItemState>(entity =>
        {
            entity.ToTable("LocalItemStates");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.RelativePath)
                .IsRequired();

            entity.HasIndex(e => e.RelativePath)
                .IsUnique();

            entity.HasIndex(e => e.ServerResourceId);
        });

        modelBuilder.Entity<SyncScope>(entity =>
        {
            entity.ToTable("SyncScopes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<SyncCursor>(entity =>
        {
            entity.ToTable("SyncCursors");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedNever();
        });
    }
}
