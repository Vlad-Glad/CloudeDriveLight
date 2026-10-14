using System.IO;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class ScopedSyncEngineTests : IDisposable
{
    private static string CloudHash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("cloud"))).ToLowerInvariant();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _root = Guid.NewGuid();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gdl-scope-{Guid.NewGuid():N}");
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly Mock<IDeviceIdentifierService> _device = new();
    private readonly LocalSyncDbContextFactory _factory;
    private readonly SyncEngine _engine;
    private string LocalPath => Path.Combine(_directory, "local");

    public ScopedSyncEngineTests()
    {
        Directory.CreateDirectory(_directory);
        _factory = new LocalSyncDbContextFactory(Path.Combine(_directory, "db"));
        _device.Setup(d => d.GetDeviceId()).Returns(Guid.NewGuid());
        _engine = new SyncEngine(_api.Object, _factory, _device.Object);
        _api.Setup(a => a.GetChangesAsync(0, 1000, It.IsAny<CancellationToken>())).ReturnsAsync(new SyncChangesResponse(new List<ChangeResponse>(), 10, false));
        _api.Setup(a => a.GetFolderContentAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        _api.Setup(a => a.GetFilesAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        _api.Setup(a => a.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((_, path, _) => File.WriteAllTextAsync(path, "cloud"));
    }

    private ChangeResponse Change(long id, Guid resource, ResourceType type, ChangeType eventType, string name, Guid? parent)
        => new(id, resource, type, eventType, null, parent, name, CloudHash, 5, DateTime.UtcNow);

    private void Delta(params ChangeResponse[] changes) =>
        _api.Setup(a => a.GetChangesAsync(10, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncChangesResponse(changes.ToList(), changes.Last().Id, false));

    [Fact]
    public async Task InitialSyncUsesOnlyDeviceTree()
    {
        Assert.True((await _engine.SynchronizeAsync(_user, LocalPath, default, _root)).Success);
        _api.Verify(a => a.GetFolderContentAsync(null, It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Never);
        using var db = _factory.CreateDbContext(_user);
        Assert.Equal(_root, (await db.Scopes.SingleAsync()).ServerRootFolderId);
        Assert.Equal(string.Empty, (await db.Items.SingleAsync()).RelativePath);
    }

    [Fact]
    public async Task DeltaSkipsHomeAndOtherDeviceButAppliesNestedCreationInSamePage()
    {
        await _engine.SynchronizeAsync(_user, LocalPath, default, _root);
        var folder = Guid.NewGuid();
        var file = Guid.NewGuid();
        Delta(Change(11, Guid.NewGuid(), ResourceType.File, ChangeType.Created, "home.txt", null),
              Change(12, Guid.NewGuid(), ResourceType.File, ChangeType.Created, "other.txt", Guid.NewGuid()),
              Change(13, folder, ResourceType.Folder, ChangeType.Created, "Docs", _root),
              Change(14, file, ResourceType.File, ChangeType.Created, "notes.txt", folder));
        var result = await _engine.SynchronizeAsync(_user, LocalPath, default, _root);
        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.ProcessedChangesCount);
        Assert.True(File.Exists(Path.Combine(LocalPath, "Docs", "notes.txt")));
        Assert.False(File.Exists(Path.Combine(LocalPath, "home.txt")));
        using var db = _factory.CreateDbContext(_user);
        Assert.Equal(14, (await db.Cursors.SingleAsync()).LastProcessedChangeId);
        Assert.Equal("Docs/notes.txt", (await db.Items.SingleAsync(i => i.ServerResourceId == file)).RelativePath);
    }

    [Fact]
    public async Task PathChangeRebuildsStateAndPopulatesNewFolderWithoutDeletingOldFiles()
    {
        var file = Guid.NewGuid();
        _api.Setup(a => a.GetFilesAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new FileResponse(file, "notes.txt", 1, CloudHash, 5, _root, _user, DateTime.UtcNow, null, "internal")
        });
        Assert.True((await _engine.SynchronizeAsync(_user, LocalPath, default, _root)).Success);
        var newPath = Path.Combine(_directory, "new-local");
        Assert.True((await _engine.SynchronizeAsync(_user, newPath, default, _root)).Success);
        Assert.True(File.Exists(Path.Combine(LocalPath, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(newPath, "notes.txt")));
        using var db = _factory.CreateDbContext(_user);
        Assert.Equal(Path.GetFullPath(newPath), (await db.Scopes.SingleAsync()).LocalPath);
    }

    [Fact]
    public async Task RenamingFolderUpdatesDescendantPathsBeforeFollowingModification()
    {
        await _engine.SynchronizeAsync(_user, LocalPath, default, _root);
        var folder = Guid.NewGuid();
        var file = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(LocalPath, "Old"));
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "Old", "notes.txt"), "old");
        using (var db = _factory.CreateDbContext(_user))
        {
            db.Items.AddRange(new LocalItemState { ServerResourceId = folder, IsDirectory = true, RelativePath = "Old" },
                new LocalItemState { ServerResourceId = file, RelativePath = "Old/notes.txt" });
            await db.SaveChangesAsync();
        }
        Delta(Change(11, folder, ResourceType.Folder, ChangeType.Renamed, "New", _root),
              Change(12, file, ResourceType.File, ChangeType.Modified, "notes.txt", folder));
        Assert.True((await _engine.SynchronizeAsync(_user, LocalPath, default, _root)).Success);
        Assert.Equal("cloud", await File.ReadAllTextAsync(Path.Combine(LocalPath, "New", "notes.txt")));
        Assert.False(Directory.Exists(Path.Combine(LocalPath, "Old")));
        using var state = _factory.CreateDbContext(_user);
        Assert.Equal("New/notes.txt", (await state.Items.SingleAsync(i => i.ServerResourceId == file)).RelativePath);
    }

    [Fact]
    public async Task FailedInitialDownloadCanBeRetriedWithoutLosingExistingItems()
    {
        var file = Guid.NewGuid();
        _api.Setup(a => a.GetFilesAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new FileResponse(file, "notes.txt", 1, CloudHash, 5, _root, _user, DateTime.UtcNow, null, "internal")
        });
        _api.SetupSequence(a => a.DownloadFileAsync(file, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("locked"))
            .Returns(() => File.WriteAllTextAsync(Path.Combine(LocalPath, "notes.txt"), "cloud"));
        Assert.False((await _engine.SynchronizeAsync(_user, LocalPath, default, _root)).Success);
        Assert.True((await _engine.SynchronizeAsync(_user, LocalPath, default, _root)).Success);
        using var db = _factory.CreateDbContext(_user);
        Assert.True((await db.Scopes.SingleAsync()).InitialSyncCompleted);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }
}
