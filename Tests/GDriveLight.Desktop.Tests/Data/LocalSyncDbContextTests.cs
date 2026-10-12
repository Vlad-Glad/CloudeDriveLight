using System.IO;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Sync;
using Microsoft.Data.Sqlite;

using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Tests.Data;

public class LocalSyncDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LocalSyncDbContext> _options;

    public LocalSyncDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LocalSyncDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new LocalSyncDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task CanInsertAndRetrieveLocalItemState()
    {
        var resourceId = Guid.NewGuid();
        var item = new LocalItemState
        {
            ServerResourceId = resourceId,
            IsDirectory = false,
            RelativePath = "documents/test.txt",
            LocalModifiedAtUtc = DateTime.UtcNow,
            LocalSize = 1024L,
            LastSyncedHash = "abc123hash",
            Status = SyncStatus.Synced
        };

        using (var context = new LocalSyncDbContext(_options))
        {
            context.Items.Add(item);
            await context.SaveChangesAsync();
        }

        using (var context = new LocalSyncDbContext(_options))
        {
            var saved = await context.Items.FirstOrDefaultAsync(x => x.ServerResourceId == resourceId);
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal("documents/test.txt", saved.RelativePath);
            Assert.False(saved.IsDirectory);
            Assert.Equal(1024L, saved.LocalSize);
            Assert.Equal("abc123hash", saved.LastSyncedHash);
            Assert.Equal(SyncStatus.Synced, saved.Status);
        }
    }

    [Fact]
    public async Task DuplicateRelativePath_ThrowsDbUpdateException()
    {
        var item1 = new LocalItemState
        {
            RelativePath = "same/path.txt",
            Status = SyncStatus.Synced
        };

        var item2 = new LocalItemState
        {
            RelativePath = "same/path.txt",
            Status = SyncStatus.PendingUpload
        };

        using (var context = new LocalSyncDbContext(_options))
        {
            context.Items.Add(item1);
            await context.SaveChangesAsync();
        }

        using (var context = new LocalSyncDbContext(_options))
        {
            context.Items.Add(item2);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task CanInsertAndQueryByServerResourceId()
    {
        var resourceId = Guid.NewGuid();
        var item = new LocalItemState
        {
            ServerResourceId = resourceId,
            RelativePath = "folder",
            IsDirectory = true,
            Status = SyncStatus.Synced
        };

        using (var context = new LocalSyncDbContext(_options))
        {
            context.Items.Add(item);
            await context.SaveChangesAsync();
        }

        using (var context = new LocalSyncDbContext(_options))
        {
            var found = await context.Items.FirstOrDefaultAsync(x => x.ServerResourceId == resourceId);
            Assert.NotNull(found);
            Assert.Equal("folder", found.RelativePath);
        }
    }

    [Fact]
    public async Task CanInsertAndTrackSyncCursor()
    {
        var cursor = new SyncCursor
        {
            Id = 1,
            LastProcessedChangeId = 150L
        };

        using (var context = new LocalSyncDbContext(_options))
        {
            context.Cursors.Add(cursor);
            await context.SaveChangesAsync();
        }

        using (var context = new LocalSyncDbContext(_options))
        {
            var saved = await context.Cursors.FindAsync(1);
            Assert.NotNull(saved);
            Assert.Equal(150L, saved.LastProcessedChangeId);

            saved.LastProcessedChangeId = 200L;
            await context.SaveChangesAsync();
        }

        using (var context = new LocalSyncDbContext(_options))
        {
            var updated = await context.Cursors.FindAsync(1);
            Assert.NotNull(updated);
            Assert.Equal(200L, updated.LastProcessedChangeId);
        }
    }

    [Fact]
    public void Factory_GeneratesDefaultDatabasePathInLocalAppData()
    {
        var factory = new LocalSyncDbContextFactory();
        var userId = Guid.NewGuid();

        var path = factory.GetDatabasePath(userId);

        var expectedFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GDriveLight");

        Assert.Equal(Path.Combine(expectedFolder, $"sync_{userId}.db"), path);
    }

    [Fact]
    public void Factory_WithCustomBaseDirectory_CreatesDatabaseFileAndSchema()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "GDriveLightTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new LocalSyncDbContextFactory(tempDir);
            var userId = Guid.NewGuid();

            using var context = factory.CreateDbContext(userId);

            var dbPath = factory.GetDatabasePath(userId);
            Assert.True(File.Exists(dbPath));

            context.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 42L });
            context.SaveChanges();

            var cursor = context.Cursors.Find(1);
            Assert.NotNull(cursor);
            Assert.Equal(42L, cursor.LastProcessedChangeId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                SqliteConnection.ClearAllPools();
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Ignore transient file lock
                }
            }
        }
    }
}
