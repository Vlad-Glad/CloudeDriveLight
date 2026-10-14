using System.IO;
using System.Security.Cryptography;
using System.Text;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using GDriveLight.Desktop.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class TwoWaySyncEngineTests : IDisposable
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _root = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gdl-two-way-{Guid.NewGuid():N}");
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly Dictionary<Guid, FolderResponse> _folders = new();
    private readonly Dictionary<Guid, FileResponse> _files = new();
    private readonly Dictionary<Guid, byte[]> _content = new();
    private readonly List<ChangeResponse> _changes = new();
    private readonly LocalSyncDbContextFactory _factory;
    private readonly SyncEngine _engine;
    private string LocalPath => Path.Combine(_directory, "local");

    public TwoWaySyncEngineTests()
    {
        Directory.CreateDirectory(LocalPath);
        _factory = new LocalSyncDbContextFactory(Path.Combine(_directory, "db"));
        var identifiers = new Mock<IDeviceIdentifierService>();
        identifiers.Setup(d => d.GetDeviceId()).Returns(_device);
        _engine = new SyncEngine(_api.Object, _factory, identifiers.Object);
        _api.SetupGet(a => a.CurrentUserId).Returns(_user);
        _api.SetupGet(a => a.DeviceId).Returns(_device);
        _api.Setup(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_device);
        _api.Setup(a => a.ConfigureSyncFolderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((path, _) => Task.FromResult(new DeviceResponse(_device, "Laptop", _root, path)));
        _api.Setup(a => a.GetDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new[] { new DeviceResponse(_device, "Laptop", _root, LocalPath) });
        _api.Setup(a => a.GetFolderContentAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns<Guid?, CancellationToken>((parent, _) => Task.FromResult<IEnumerable<FolderResponse>>(_folders.Values.Where(f => f.ParentFolderId == parent).ToList()));
        _api.Setup(a => a.GetFilesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns<Guid?, CancellationToken>((parent, _) => Task.FromResult<IEnumerable<FileResponse>>(_files.Values.Where(f => f.FolderId == parent).ToList()));
        _api.Setup(a => a.GetChangesAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<long, int, CancellationToken>((cursor, limit, _) =>
            {
                var changes = _changes.Where(c => c.Id > cursor).Take(limit).ToList();
                return Task.FromResult(new SyncChangesResponse(changes, changes.LastOrDefault()?.Id ?? cursor, changes.Count == limit));
            });
        _api.Setup(a => a.CreateFolderAsync(It.IsAny<string>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .Returns<string, Guid?, bool, CancellationToken>((name, parent, _, _) =>
            {
                Assert.True(parent.HasValue);
                var folder = new FolderResponse(Guid.NewGuid(), name, parent, _user);
                _folders.Add(folder.Id, folder);
                AddChange(folder.Id, ResourceType.Folder, ChangeType.Created, name, parent, null, null, _device);
                return Task.FromResult(folder.Id);
            });
        _api.Setup(a => a.UploadFileAsync(It.IsAny<string>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .Returns<string, Guid?, bool, CancellationToken>((path, parent, _, _) =>
            {
                Assert.True(parent.HasValue);
                return Task.FromResult(AddFile(Path.GetFileName(path), File.ReadAllBytes(path), parent!.Value, _device));
            });
        _api.Setup(a => a.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((id, path, token) => File.WriteAllBytesAsync(path, _content[id], token));
        _api.Setup(a => a.UpdateFileContentAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Stream, string, CancellationToken>(async (id, stream, _, token) =>
            {
                using var output = new MemoryStream();
                await stream.CopyToAsync(output, token);
                ModifyCloudFile(id, output.ToArray(), _device);
            });
        _api.Setup(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<bool>(), true, It.IsAny<CancellationToken>()))
            .Returns<Guid, bool, bool, CancellationToken>((id, folder, _, _) =>
            {
                DeleteCloudItem(id, folder, _device);
                return Task.CompletedTask;
            });
    }

    private Task<SyncResult> Sync() => _engine.SynchronizeAsync(_user, LocalPath, default, _root);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private void AddChange(Guid id, ResourceType type, ChangeType change, string name, Guid? parent, string? hash, long? size, Guid? origin)
        => _changes.Add(new(_changes.Count + 1, id, type, change, origin, parent, name, hash, size, DateTime.UtcNow));
    private Guid AddFile(string name, byte[] bytes, Guid parent, Guid? origin = null)
    {
        var file = new FileResponse(Guid.NewGuid(), name, 1, Hash(bytes), bytes.Length, parent, _user, DateTime.UtcNow, null, "internal");
        _files.Add(file.Id, file);
        _content.Add(file.Id, bytes);
        AddChange(file.Id, ResourceType.File, ChangeType.Created, name, parent, file.ContentHash, file.SizeBytes, origin);
        return file.Id;
    }
    private void ModifyCloudFile(Guid id, byte[] bytes, Guid? origin = null)
    {
        var file = _files[id] with { ContentHash = Hash(bytes), SizeBytes = bytes.Length, EditedAtUtc = DateTime.UtcNow };
        _files[id] = file;
        _content[id] = bytes;
        AddChange(id, ResourceType.File, ChangeType.Modified, file.Name, file.FolderId, file.ContentHash, file.SizeBytes, origin);
    }
    private void DeleteCloudItem(Guid id, bool isFolder, Guid? origin = null)
    {
        if (isFolder)
        {
            var folder = _folders[id];
            AddChange(id, ResourceType.Folder, ChangeType.Deleted, folder.Name, folder.ParentFolderId, null, null, origin);
            foreach (var child in _folders.Values.Where(f => f.ParentFolderId == id).ToList()) DeleteCloudItem(child.Id, true, origin);
            foreach (var child in _files.Values.Where(f => f.FolderId == id).ToList()) DeleteCloudItem(child.Id, false, origin);
            _folders.Remove(id);
        }
        else
        {
            var file = _files[id];
            _files.Remove(id);
            _content.Remove(id);
            AddChange(id, ResourceType.File, ChangeType.Deleted, file.Name, file.FolderId, file.ContentHash, file.SizeBytes, origin);
        }
    }

    [Fact]
    public async Task FirstSyncUploadsNestedFilesAndEmptyFoldersOnlyToDeviceRoot()
    {
        Directory.CreateDirectory(Path.Combine(LocalPath, "Docs", "Nested"));
        Directory.CreateDirectory(Path.Combine(LocalPath, "Empty"));
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "Docs", "Nested", "notes.txt"), "local");
        Assert.True((await Sync()).Success);
        Assert.Equal(3, _folders.Count);
        Assert.Equal("local", Encoding.UTF8.GetString(_content[_files.Single().Key]));
        var docs = _folders.Values.Single(f => f.Name == "Docs");
        var nested = _folders.Values.Single(f => f.Name == "Nested");
        Assert.Equal(_root, docs.ParentFolderId);
        Assert.Equal(docs.Id, nested.ParentFolderId);
        Assert.Equal(nested.Id, _files.Single().Value.FolderId);
    }

    [Fact]
    public async Task RepeatedSyncDoesNotDuplicateFilesOrReuploadOwnEcho()
    {
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "notes.txt"), "local");
        Assert.True((await Sync()).Success);
        Assert.True((await Sync()).Success);
        Assert.Single(_files);
        _api.Verify(a => a.UploadFileAsync(It.IsAny<string>(), _root, true, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LocalContentChangeUpdatesExistingResource()
    {
        var path = Path.Combine(LocalPath, "notes.txt");
        await File.WriteAllTextAsync(path, "first");
        await Sync();
        var id = _files.Single().Key;
        await File.WriteAllTextAsync(path, "second");
        Assert.True((await Sync()).Success);
        Assert.Equal("second", Encoding.UTF8.GetString(_content[id]));
        Assert.Single(_files);
        _api.Verify(a => a.UpdateFileContentAsync(id, It.IsAny<Stream>(), "notes.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocalFileDeletionRemovesCloudResourceOnce()
    {
        var path = Path.Combine(LocalPath, "notes.txt");
        await File.WriteAllTextAsync(path, "local");
        await Sync();
        var id = _files.Single().Key;
        File.Delete(path);
        Assert.True((await Sync()).Success);
        Assert.True((await Sync()).Success);
        Assert.Empty(_files);
        _api.Verify(a => a.DeleteAsync(id, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocalFolderDeletionUsesSingleCascadeAndRemovesDescendantState()
    {
        var path = Path.Combine(LocalPath, "Docs");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "notes.txt"), "local");
        await Sync();
        var id = _folders.Single().Key;
        Directory.Delete(path, true);
        Assert.True((await Sync()).Success);
        Assert.Empty(_files);
        Assert.Empty(_folders);
        _api.Verify(a => a.DeleteAsync(id, true, true, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), false, true, It.IsAny<CancellationToken>()), Times.Never);
        using var db = _factory.CreateDbContext(_user);
        Assert.Equal(_root, (await db.Items.SingleAsync()).ServerResourceId);
    }

    [Fact]
    public async Task CloudChangeDownloadsWithoutBeingUploadedBack()
    {
        var path = Path.Combine(LocalPath, "notes.txt");
        await File.WriteAllTextAsync(path, "first");
        await Sync();
        var id = _files.Single().Key;
        // Consume the local creation echo before a later cloud modification.
        await Sync();
        ModifyCloudFile(id, Encoding.UTF8.GetBytes("cloud edit"));
        Assert.True((await Sync()).Success);
        Assert.Equal("cloud edit", await File.ReadAllTextAsync(path));
        _api.Verify(a => a.UpdateFileContentAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimultaneousLocalAndCloudEditsPreserveBothVersionsAndReportConflict()
    {
        var path = Path.Combine(LocalPath, "notes.txt");
        await File.WriteAllTextAsync(path, "first");
        await Sync();
        await Sync();
        var id = _files.Single().Key;
        await File.WriteAllTextAsync(path, "local edit");
        ModifyCloudFile(id, Encoding.UTF8.GetBytes("cloud edit"));
        Assert.False((await Sync()).Success);
        Assert.Equal("local edit", await File.ReadAllTextAsync(path));
        Assert.Equal("cloud edit", Encoding.UTF8.GetString(_content[id]));
        using var db = _factory.CreateDbContext(_user);
        Assert.Equal(SyncStatus.Conflict, (await db.Items.SingleAsync(s => s.ServerResourceId == id)).Status);
        Assert.False((await Sync()).Success);
    }

    [Fact]
    public async Task InitialNameCollisionDoesNotOverwriteLocalFile()
    {
        var path = Path.Combine(LocalPath, "notes.txt");
        await File.WriteAllTextAsync(path, "local");
        AddFile("notes.txt", Encoding.UTF8.GetBytes("cloud"), _root);
        Assert.False((await Sync()).Success);
        Assert.Equal("local", await File.ReadAllTextAsync(path));
        Assert.Equal("cloud", Encoding.UTF8.GetString(_content.Single().Value));
    }

    [Fact]
    public async Task MissingRootIsNotTreatedAsDeletionOfWholeCloudTree()
    {
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "notes.txt"), "local");
        await Sync();
        Directory.Delete(LocalPath, true);
        var result = await Sync();
        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message);
        Assert.Single(_files);
        _api.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<bool>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyFilesAreUploadedAndDoNotCauseRepeatedTransfers()
    {
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "empty.txt"), string.Empty);
        Assert.True((await Sync()).Success);
        Assert.True((await Sync()).Success);
        Assert.Empty(_content.Single().Value);
    }

    [Fact]
    public async Task OneUploadFailureDoesNotPreventOtherFilesFromBeingUploaded()
    {
        var broken = Path.Combine(LocalPath, "broken.txt");
        await File.WriteAllTextAsync(broken, "broken");
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "ok.txt"), "ok");
        _api.Setup(a => a.UploadFileAsync(broken, _root, true, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("locked"));
        var result = await Sync();
        Assert.False(result.Success);
        Assert.Equal("ok.txt", _files.Single().Value.Name);
    }

    [Fact]
    public async Task ManualSyncUploadsEightLocalFilesAndRefreshesSyncedTab()
    {
        for (var i = 0; i < 8; i++) await File.WriteAllTextAsync(Path.Combine(LocalPath, $"photo-{i}.jpg"), $"image {i}");
        var settings = new SyncSettingsService(Path.Combine(_directory, "settings"));
        settings.SetSyncFolderPath(_user, LocalPath);
        using var vm = new MainWindowViewModel(_api.Object, new Mock<IFileDialogService>().Object,
            new Mock<IUserDialogService>().Object, _engine, settings, _factory);
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        Assert.True(vm.IsEmpty);
        await vm.SyncCommand.ExecuteAsync();
        Assert.False(vm.IsNotificationError, vm.NotificationMessage);
        Assert.Equal(8, vm.VisibleFiles.Count);
        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsContentVisible);
        Assert.All(vm.VisibleFiles, file => Assert.Equal(_root, file.FolderId));
        await vm.SyncCommand.ExecuteAsync();
        Assert.Equal(8, vm.VisibleFiles.Count);
        Assert.Equal(8, _files.Count);
    }

    [Fact]
    public async Task CloudCascadeDeletionPreservesFolderContainingNewLocalItems()
    {
        var directory = Path.Combine(LocalPath, "Docs");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "synced.txt"), "synced");
        await Sync();
        await Sync();
        var id = _folders.Single().Key;
        await File.WriteAllTextAsync(Path.Combine(directory, "new.txt"), "new local content");
        DeleteCloudItem(id, true);
        Assert.False((await Sync()).Success);
        Assert.Equal("new local content", await File.ReadAllTextAsync(Path.Combine(directory, "new.txt")));
        Assert.Equal("synced", await File.ReadAllTextAsync(Path.Combine(directory, "synced.txt")));
        Assert.Empty(_files);
    }

    [Fact]
    public async Task LocalRenameIsReflectedInCloudWithoutDuplicateOldName()
    {
        var oldPath = Path.Combine(LocalPath, "old.txt");
        await File.WriteAllTextAsync(oldPath, "local content");
        await Sync();
        await Sync();
        var newPath = Path.Combine(LocalPath, "new.txt");
        File.Move(oldPath, newPath);
        Assert.True((await Sync()).Success);
        Assert.Equal("new.txt", _files.Single().Value.Name);
        Assert.Equal("local content", Encoding.UTF8.GetString(_content.Single().Value));
        Assert.True((await Sync()).Success);
        Assert.True(File.Exists(newPath));
        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public async Task NewCloudFileIsDownloadedThenShownWithoutBeingUploadedAgain()
    {
        Assert.True((await Sync()).Success);
        AddFile("cloud.txt", Encoding.UTF8.GetBytes("cloud content"), _root);
        Assert.True((await Sync()).Success);
        Assert.Equal("cloud content", await File.ReadAllTextAsync(Path.Combine(LocalPath, "cloud.txt")));
        _api.Verify(a => a.UploadFileAsync(It.IsAny<string>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedLocalRenameUploadPreservesCloudSourceUntilRetry()
    {
        var oldPath = Path.Combine(LocalPath, "old.txt");
        await File.WriteAllTextAsync(oldPath, "local content");
        await Sync();
        await Sync();
        var newPath = Path.Combine(LocalPath, "new.txt");
        File.Move(oldPath, newPath);
        _api.Setup(a => a.UploadFileAsync(newPath, _root, true, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("transfer failed"));
        Assert.False((await Sync()).Success);
        Assert.Equal("old.txt", _files.Single().Value.Name);
        Assert.Equal("local content", await File.ReadAllTextAsync(newPath));
        _api.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), false, true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConcurrentManualSyncCallsDoNotUploadDuplicates()
    {
        await File.WriteAllTextAsync(Path.Combine(LocalPath, "notes.txt"), "local");
        var response = new TaskCompletionSource<SyncChangesResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Setup(a => a.GetChangesAsync(0, 1000, It.IsAny<CancellationToken>())).Returns(response.Task);
        var first = Sync();
        var second = await Sync();
        Assert.False(second.Success);
        Assert.Contains("already running", second.Message);
        response.SetResult(new SyncChangesResponse([], 0, false));
        Assert.True((await first).Success);
        _api.Verify(a => a.UploadFileAsync(It.IsAny<string>(), _root, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }
}
