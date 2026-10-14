using System.IO;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class SyncedFoldersViewModelTests : IDisposable
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly Guid _rootId = Guid.NewGuid();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gdl-view-{Guid.NewGuid():N}");
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly Mock<IFileDialogService> _files = new();
    private readonly Mock<IUserDialogService> _dialogs = new();
    private readonly Mock<ISyncSettingsService> _settings = new();

    public SyncedFoldersViewModelTests()
    {
        Directory.CreateDirectory(_directory);
        _api.SetupGet(a => a.CurrentUserId).Returns(_userId);
        _api.SetupGet(a => a.DeviceId).Returns(_deviceId);
        _api.Setup(a => a.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new DeviceResponse(_deviceId, "Laptop", _rootId, _directory),
            new DeviceResponse(Guid.NewGuid(), "Other device", Guid.NewGuid(), @"D:\Remote")
        });
        _api.Setup(a => a.GetFolderContentAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        _api.Setup(a => a.GetFilesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        _settings.Setup(s => s.GetSyncFolderPath(_userId)).Returns(_directory);
    }

    private MainWindowViewModel Create() => new(_api.Object, _files.Object, _dialogs.Object, syncSettings: _settings.Object);

    [Fact]
    public async Task SyncedTabLoadsSelectedDeviceRootAndLocalPath()
    {
        var fileId = Guid.NewGuid();
        _api.Setup(a => a.GetFilesAsync(_rootId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { File(fileId, _rootId) });
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        Assert.True(vm.IsCurrentDevice);
        Assert.Equal(_rootId, vm.Breadcrumbs.Single().Id);
        Assert.Equal(_directory, vm.CurrentSyncFolderPath);
        Assert.Equal(fileId, vm.VisibleFiles.Single().Id);
        _api.Verify(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchingDevicesResetsBreadcrumbsAndDisablesLocalConfiguration()
    {
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        vm.SelectedDevice = vm.Devices.Single(d => !d.IsCurrentDevice);
        await vm.RefreshAsync();
        Assert.Equal(vm.SelectedDevice!.SyncRootFolderId, vm.Breadcrumbs.Single().Id);
        Assert.Equal(@"D:\Remote", vm.CurrentSyncFolderPath);
        Assert.False(vm.ChangeSyncFolderCommand.CanExecute(null));
        Assert.False(vm.OpenSyncFolderInExplorerCommand.CanExecute(null));
        Assert.True(vm.CreateFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreateAndUploadAreCloudActionsWithoutLocalWrites()
    {
        _dialogs.Setup(d => d.RequestName("New folder", "Folder name", It.IsAny<string>())).Returns("Created in cloud");
        _api.Setup(a => a.CreateFolderAsync("Created in cloud", _rootId, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var source = Path.Combine(_directory, "upload.txt");
        _files.Setup(d => d.SelectFilesToUpload()).Returns(new[] { source });
        _api.Setup(a => a.UploadFileAsync(source, _rootId, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        await vm.CreateFolderCommand.ExecuteAsync();
        await vm.UploadCommand.ExecuteAsync();
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
        _api.Verify(a => a.CreateFolderAsync("Created in cloud", _rootId, It.IsAny<CancellationToken>()), Times.Once);
        _api.Verify(a => a.UploadFileAsync(source, _rootId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferFromSyncedFolderToHomeCreatesCopyAndPreservesSource()
    {
        var fileId = Guid.NewGuid();
        _api.Setup(a => a.GetFilesAsync(_rootId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { File(fileId, _rootId) });
        _api.Setup(a => a.DownloadFileAsync(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((_, path, _) => System.IO.File.WriteAllTextAsync(path, "content"));
        _api.Setup(a => a.UploadFileAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        await vm.MoveToCommand.ExecuteAsync(new ItemMoveRequest(vm.Files.Single(), new FolderDestination(null, "Home")));
        Assert.Contains("Copied", vm.NotificationMessage);
        _api.Verify(a => a.MoveAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangingLocalPathPersistsDeviceConfiguration()
    {
        var target = Path.Combine(_directory, "new-location");
        _files.Setup(f => f.SelectFolder(_directory)).Returns(target);
        _api.Setup(a => a.ConfigureSyncFolderAsync(target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceResponse(_deviceId, "Laptop", _rootId, target));
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        await vm.ChangeSyncFolderCommand.ExecuteAsync();
        _settings.Verify(s => s.SetSyncFolderPath(_userId, target), Times.Once);
        Assert.Contains(target, vm.NotificationMessage);
    }

    [Fact]
    public async Task DeviceWithoutConfiguredRootNeverFallsBackToHome()
    {
        _api.Setup(a => a.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DeviceResponse(_deviceId, "Laptop", null, null) });
        using var vm = Create();
        vm.SelectedSidebarIndex = 1;
        await vm.RefreshAsync();
        Assert.False(vm.HasSyncRoot);
        Assert.False(vm.UploadCommand.CanExecute(null));
        Assert.True(vm.ChangeSyncFolderCommand.CanExecute(null));
        _api.Verify(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Never);
    }

    private FileResponse File(Guid id, Guid parent) => new(id, "notes.txt", 1, "hash", 3, parent, _userId, DateTime.UtcNow, null, "internal");

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }
}
