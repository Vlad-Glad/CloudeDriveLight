using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Tests.Infrastructure;

public sealed class SyncedFolderIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _home = Guid.NewGuid();
    private readonly Guid _sync = Guid.NewGuid();
    private readonly Guid _nested = Guid.NewGuid();

    public SyncedFolderIsolationTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var device = Guid.NewGuid();
        _db.Users.Add(new ApplicationUserEntity { Id = _user });
        _db.Devices.Add(new DeviceEntity { Id = device, UserId = _user, Name = "Laptop" });
        _db.DriveFolders.AddRange(
            new DriveFolderEntity { Id = _home, OwnerId = _user, Name = "match Home" },
            new DriveFolderEntity { Id = _sync, OwnerId = _user, Name = "match Sync" },
            new DriveFolderEntity { Id = _nested, OwnerId = _user, Name = "match Nested", ParentFolderId = _sync });
        _db.SyncFolders.Add(new SyncFolderEntity { Id = Guid.NewGuid(), DeviceId = device, DriveFolderId = _sync, LocalPath = @"C:\Sync" });
        _db.DriveFiles.AddRange(
            new DriveFileEntity { Id = Guid.NewGuid(), OwnerId = _user, Name = "match Root.txt", FileTypeId = 1, FileUrl = "storage/file", ContentHash = "hash" },
            new DriveFileEntity { Id = Guid.NewGuid(), OwnerId = _user, Name = "match Home.txt", FolderId = _home, FileTypeId = 1, FileUrl = "storage/file", ContentHash = "hash" },
            new DriveFileEntity { Id = Guid.NewGuid(), OwnerId = _user, Name = "match Sync.txt", FolderId = _sync, FileTypeId = 1, FileUrl = "storage/file", ContentHash = "hash" },
            new DriveFileEntity { Id = Guid.NewGuid(), OwnerId = _user, Name = "match Nested.txt", FolderId = _nested, FileTypeId = 1, FileUrl = "storage/file", ContentHash = "hash" });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task StorageUsageIncludesSyncedTreesAndExcludesOtherOwners()
    {
        await _db.DriveFiles.ExecuteUpdateAsync(set => set.SetProperty(file => file.SizeBytes, 1024L));
        var otherUser = Guid.NewGuid();
        _db.Users.Add(new ApplicationUserEntity { Id = otherUser });
        _db.DriveFiles.Add(new DriveFileEntity
        {
            Id = Guid.NewGuid(), OwnerId = otherUser, FileTypeId = 1,
            Name = "Other.txt", FileUrl = "other", ContentHash = "hash", SizeBytes = 9000
        });
        await _db.SaveChangesAsync();
        var repository = new DriveFileRepository(_db);
        Assert.Equal(4096, await repository.GetUsedBytesForUserAsync(_user));
        Assert.Equal(9000, await repository.GetUsedBytesForUserAsync(otherUser));
        Assert.Equal(0, await repository.GetUsedBytesForUserAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HomeRootExcludesSyncedRootsWhileDeviceContentsRemainAccessible()
    {
        var repository = new DriveFolderRepository(_db);
        Assert.Equal(_home, (await repository.GetRootFoldersForUserAsync(_user)).Single().Id);
        Assert.Equal(_nested, (await repository.GetSubFoldersAsync(_sync)).Single().Id);
    }

    [Fact]
    public async Task HomeFolderSearchExcludesWholeSyncedTree()
    {
        Assert.Equal(_home, (await new DriveFolderRepository(_db).SearchByNameAsync("match", _user)).Single().Id);
    }

    [Fact]
    public async Task HomeFileSearchExcludesSyncedRootAndItsDescendants()
    {
        var files = (await new DriveFileRepository(_db).SearchByNameAsync("match", _user)).ToList();
        Assert.Equal(2, files.Count);
        Assert.DoesNotContain(files, file => file.FolderId == _sync || file.FolderId == _nested);
    }

    [Fact]
    public async Task DeviceBindingRoundTripsReportedLocalPath()
    {
        var binding = (await new SyncFolderRepository(_db).GetByDeviceIdAsync((await _db.Devices.SingleAsync()).Id)).Single();
        Assert.Equal(_sync, binding.DriveFolderId);
        Assert.Equal(@"C:\Sync", binding.LocalPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
