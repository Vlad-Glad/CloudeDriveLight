using System.IO;
using System.Security.Cryptography;
using System.Text;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GDriveLight.Desktop.Tests.Services;

public class SyncEngineTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _syncFolder;
    private readonly string _dbFolder;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _myDeviceId = Guid.NewGuid();
    private readonly Guid _remoteDeviceId = Guid.NewGuid();
    private readonly Mock<IDriveApiService> _apiMock = new(MockBehavior.Strict);
    private readonly Mock<IDeviceIdentifierService> _deviceMock = new(MockBehavior.Strict);
    private readonly LocalSyncDbContextFactory _dbFactory;

    public SyncEngineTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "GDriveLightTests_SyncEngine_" + Guid.NewGuid());
        _syncFolder = Path.Combine(_testBaseDir, "Sync");
        _dbFolder = Path.Combine(_testBaseDir, "Db");
        Directory.CreateDirectory(_syncFolder);
        Directory.CreateDirectory(_dbFolder);

        _deviceMock.Setup(d => d.GetDeviceId()).Returns(_myDeviceId);
        _dbFactory = new LocalSyncDbContextFactory(_dbFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBaseDir))
        {
            try
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task InitialSync_TraversesServerTree_DownloadsFiles_AndAdvancesCursor()
    {
        var folderId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileContent = "Server file content";
        var fileHash = ComputeSha256(fileContent);

        // Server changelog check (to find latest cursor)
        _apiMock.Setup(a => a.GetChangesAsync(0, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse>(), 42, false));

        // Root folder content: contains 1 subfolder "Docs" and 0 files
        _apiMock.Setup(a => a.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FolderResponse(folderId, "Docs", null, _userId) });
        _apiMock.Setup(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FileResponse>());

        // Subfolder content: contains 0 subfolders and 1 file "readme.txt"
        _apiMock.Setup(a => a.GetFolderContentAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        _apiMock.Setup(a => a.GetFilesAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FileResponse(fileId, "readme.txt", fileContent.Length, fileHash, folderId, _userId, DateTime.UtcNow, null, "path")
            });

        // Download handler
        _apiMock.Setup(a => a.DownloadFileAsync(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((_, destination, _) =>
            {
                File.WriteAllText(destination, fileContent);
                return Task.CompletedTask;
            });

        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        Assert.Equal(2, result.ProcessedChangesCount); // 1 folder + 1 file

        // Check local disk
        var expectedFilePath = Path.Combine(_syncFolder, "Docs", "readme.txt");
        Assert.True(File.Exists(expectedFilePath));
        Assert.Equal(fileContent, File.ReadAllText(expectedFilePath));

        // Check SQLite DB
        using var db = _dbFactory.CreateDbContext(_userId);
        var cursor = await db.Cursors.FindAsync(1);
        Assert.NotNull(cursor);
        Assert.Equal(42, cursor.LastProcessedChangeId);

        var items = await db.Items.ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.ServerResourceId == folderId && i.IsDirectory && i.RelativePath == "Docs");
        Assert.Contains(items, i => i.ServerResourceId == fileId && !i.IsDirectory && i.RelativePath == "Docs/readme.txt");
    }

    [Fact]
    public async Task InitialSync_WhenLocalFileMatchesHash_DoesNotDownloadAgain()
    {
        var fileId = Guid.NewGuid();
        var fileContent = "Already existing identical content";
        var fileHash = ComputeSha256(fileContent);

        // Pre-create file locally
        var localFilePath = Path.Combine(_syncFolder, "existing.txt");
        File.WriteAllText(localFilePath, fileContent);

        _apiMock.Setup(a => a.GetChangesAsync(0, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse>(), 10, false));

        _apiMock.Setup(a => a.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        _apiMock.Setup(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FileResponse(fileId, "existing.txt", fileContent.Length, fileHash, null, _userId, DateTime.UtcNow, null, "path")
            });

        // DownloadFileAsync should NOT be called!
        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        _apiMock.Verify(a => a.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        using var db = _dbFactory.CreateDbContext(_userId);
        var savedItem = await db.Items.FirstOrDefaultAsync(i => i.ServerResourceId == fileId);
        Assert.NotNull(savedItem);
        Assert.Equal("existing.txt", savedItem.RelativePath);
    }

    [Fact]
    public async Task DeltaSync_EchoProtection_DoesNotOverwriteDiskWhenChangeOriginatedFromThisDevice()
    {
        var fileId = Guid.NewGuid();
        var localContent = "Local modified content by this app";
        var localFilePath = Path.Combine(_syncFolder, "doc.txt");
        File.WriteAllText(localFilePath, localContent);

        // Seed DB with cursor = 5 and existing item
        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            db.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 5 });
            db.Items.Add(new LocalItemState
            {
                ServerResourceId = fileId,
                IsDirectory = false,
                RelativePath = "doc.txt",
                LocalSize = localContent.Length,
                LastSyncedHash = "oldhash",
                Status = SyncStatus.Synced,
                LocalModifiedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Server sends Modified change with OriginDeviceId == _myDeviceId
        var change = new ChangeResponse(
            Id: 6,
            ResourceId: fileId,
            ResourceType: ResourceType.File,
            EventType: ChangeType.Modified,
            Name: "doc.txt",
            ParentFolderId: null,
            ContentHash: "new_server_hash",
            SizeBytes: localContent.Length,
            TimestampUtc: DateTime.UtcNow,
            OriginDeviceId: _myDeviceId);

        _apiMock.Setup(a => a.GetChangesAsync(5, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse> { change }, 6, false));

        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        Assert.Equal(1, result.ProcessedChangesCount);

        // DownloadFileAsync must NOT be called
        _apiMock.Verify(a => a.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // File on disk remains intact
        Assert.Equal(localContent, File.ReadAllText(localFilePath));

        // SQLite item state must be updated to new hash & cursor advanced
        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            var cursor = await db.Cursors.FindAsync(1);
            Assert.Equal(6, cursor!.LastProcessedChangeId);

            var item = await db.Items.FirstOrDefaultAsync(i => i.ServerResourceId == fileId);
            Assert.NotNull(item);
            Assert.Equal("new_server_hash", item.LastSyncedHash);
        }
    }

    [Fact]
    public async Task DeltaSync_RemoteCreated_DownloadsFileAndUpdatesDb()
    {
        var fileId = Guid.NewGuid();
        var remoteContent = "Remote user created file";
        var remoteHash = ComputeSha256(remoteContent);

        // Seed DB with cursor = 10
        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            db.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 10 });
            await db.SaveChangesAsync();
        }

        var change = new ChangeResponse(
            Id: 11,
            ResourceId: fileId,
            ResourceType: ResourceType.File,
            EventType: ChangeType.Created,
            Name: "remote.txt",
            ParentFolderId: null,
            ContentHash: remoteHash,
            SizeBytes: remoteContent.Length,
            TimestampUtc: DateTime.UtcNow,
            OriginDeviceId: _remoteDeviceId);

        _apiMock.Setup(a => a.GetChangesAsync(10, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse> { change }, 11, false));

        _apiMock.Setup(a => a.DownloadFileAsync(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((_, destination, _) =>
            {
                File.WriteAllText(destination, remoteContent);
                return Task.CompletedTask;
            });

        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        Assert.Equal(1, result.ProcessedChangesCount);

        var downloadedPath = Path.Combine(_syncFolder, "remote.txt");
        Assert.True(File.Exists(downloadedPath));
        Assert.Equal(remoteContent, File.ReadAllText(downloadedPath));

        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            var cursor = await db.Cursors.FindAsync(1);
            Assert.Equal(11, cursor!.LastProcessedChangeId);

            var item = await db.Items.FirstOrDefaultAsync(i => i.ServerResourceId == fileId);
            Assert.NotNull(item);
            Assert.Equal("remote.txt", item.RelativePath);
            Assert.Equal(remoteHash, item.LastSyncedHash);
        }
    }

    [Fact]
    public async Task DeltaSync_RemoteDeleted_DeletesFileFromDiskAndRemovesFromDb()
    {
        var fileId = Guid.NewGuid();
        var filePath = Path.Combine(_syncFolder, "to_delete.txt");
        File.WriteAllText(filePath, "goodbye");

        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            db.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 20 });
            db.Items.Add(new LocalItemState
            {
                ServerResourceId = fileId,
                IsDirectory = false,
                RelativePath = "to_delete.txt",
                LocalSize = 7,
                LastSyncedHash = "h",
                Status = SyncStatus.Synced,
                LocalModifiedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var change = new ChangeResponse(
            Id: 21,
            ResourceId: fileId,
            ResourceType: ResourceType.File,
            EventType: ChangeType.Deleted,
            Name: "to_delete.txt",
            ParentFolderId: null,
            ContentHash: null,
            SizeBytes: 0,
            TimestampUtc: DateTime.UtcNow,
            OriginDeviceId: _remoteDeviceId);

        _apiMock.Setup(a => a.GetChangesAsync(20, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse> { change }, 21, false));

        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        Assert.False(File.Exists(filePath)); // Deleted from disk!

        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            var cursor = await db.Cursors.FindAsync(1);
            Assert.Equal(21, cursor!.LastProcessedChangeId);

            var item = await db.Items.FirstOrDefaultAsync(i => i.ServerResourceId == fileId);
            Assert.Null(item); // Removed from DB!
        }
    }

    [Fact]
    public async Task DeltaSync_RemoteRenamed_MovesFileOnDiskAndUpdatesDb()
    {
        var fileId = Guid.NewGuid();
        var oldPath = Path.Combine(_syncFolder, "original.txt");
        File.WriteAllText(oldPath, "important data");

        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            db.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 30 });
            db.Items.Add(new LocalItemState
            {
                ServerResourceId = fileId,
                IsDirectory = false,
                RelativePath = "original.txt",
                LocalSize = 14,
                LastSyncedHash = "h",
                Status = SyncStatus.Synced,
                LocalModifiedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var change = new ChangeResponse(
            Id: 31,
            ResourceId: fileId,
            ResourceType: ResourceType.File,
            EventType: ChangeType.Renamed,
            Name: "renamed.txt",
            ParentFolderId: null,
            ContentHash: "h",
            SizeBytes: 14,
            TimestampUtc: DateTime.UtcNow,
            OriginDeviceId: _remoteDeviceId);

        _apiMock.Setup(a => a.GetChangesAsync(30, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse> { change }, 31, false));

        var engine = new SyncEngine(_apiMock.Object, _dbFactory, _deviceMock.Object);
        var result = await engine.SynchronizeAsync(_userId, _syncFolder);

        Assert.True(result.Success);
        var newPath = Path.Combine(_syncFolder, "renamed.txt");
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(newPath));
        Assert.Equal("important data", File.ReadAllText(newPath));

        using (var db = _dbFactory.CreateDbContext(_userId))
        {
            var cursor = await db.Cursors.FindAsync(1);
            Assert.Equal(31, cursor!.LastProcessedChangeId);

            var item = await db.Items.FirstOrDefaultAsync(i => i.ServerResourceId == fileId);
            Assert.NotNull(item);
            Assert.Equal("renamed.txt", item.RelativePath);
        }
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
