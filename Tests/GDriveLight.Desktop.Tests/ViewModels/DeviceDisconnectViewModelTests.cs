using System.Net.Http;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class DeviceDisconnectViewModelTests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _root = Guid.NewGuid();
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly Mock<IUserDialogService> _dialogs = new();
    private readonly Mock<IFileDialogService> _files = new();
    private readonly Mock<ISyncSettingsService> _settings = new(MockBehavior.Strict);
    private readonly Mock<IDeviceSyncConnection> _connection = new(MockBehavior.Strict);
    private bool _enabled = true;
    private DeviceConnectionState _state = DeviceConnectionState.Connected;

    public DeviceDisconnectViewModelTests()
    {
        _api.SetupGet(a => a.CurrentUserId).Returns(_user);
        _api.Setup(a => a.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        _api.Setup(a => a.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        _api.Setup(a => a.ConfigureSyncFolderAsync(@"C:\Sync", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceResponse(Guid.NewGuid(), "Laptop", _root, @"C:\Sync"));
        _settings.Setup(s => s.GetSyncFolderPath(_user)).Returns(@"C:\Sync");
        _dialogs.Setup(d => d.ConfirmDisconnectDevice()).Returns(true);
        _connection.Setup(c => c.InitializeAsync(_user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _connection.Setup(c => c.GetState(_user)).Returns(() => _state);
        _connection.Setup(c => c.IsSyncEnabled(_user)).Returns(() => _enabled);
        _connection.Setup(c => c.DisconnectAsync(_user, It.IsAny<CancellationToken>())).Callback(() =>
        {
            _state = DeviceConnectionState.Disconnected;
            _enabled = false;
        }).Returns(Task.CompletedTask);
    }

    private MainWindowViewModel Create(ISyncEngine? sync = null) => new(_api.Object, _files.Object, _dialogs.Object,
        syncEngine: sync, syncSettings: _settings.Object, deviceConnection: _connection.Object);

    [Fact]
    public async Task DisconnectKeepsTheAccountSignedInAndHomeUsable()
    {
        using var vm = Create(new Mock<ISyncEngine>().Object);
        await vm.InitializeAsync();
        var ended = false;
        vm.SessionEnded += (_, _) => ended = true;
        await vm.DisconnectDeviceCommand.ExecuteAsync();

        Assert.False(ended);
        Assert.Equal(_user, _api.Object.CurrentUserId);
        Assert.True(vm.IsHomeView);
        Assert.True(vm.CanManageFiles);
        Assert.False(vm.IsDeviceSyncEnabled);
        Assert.False(vm.SyncCommand.CanExecute(null));
        Assert.False(vm.DisconnectDeviceCommand.CanExecute(null));
        Assert.True(vm.ChangeSyncFolderCommand.CanExecute(null));
        Assert.Empty(vm.CurrentSyncFolderPath);
        Assert.Equal("Sync disconnected", vm.SyncStatusSummary);
        Assert.Equal("Sync is off for this device", vm.SyncStatusText);
        Assert.True(vm.ShowSyncSetup);
        Assert.False(vm.ShowSyncAction);
        _api.Verify(a => a.Logout(), Times.Never);
    }

    [Fact]
    public async Task DisconnectCancelsAndWaitsForTheActiveSyncBeforeClearingMetadata()
    {
        var stopped = new TaskCompletionSource<SyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var engine = new Mock<ISyncEngine>(MockBehavior.Strict);
        engine.Setup(s => s.SynchronizeAsync(_user, @"C:\Sync", It.IsAny<CancellationToken>(), _root))
            .Callback<Guid, string, CancellationToken, Guid?>((_, _, cancellation, _) => token = cancellation).Returns(stopped.Task);
        using var vm = Create(engine.Object);
        await vm.InitializeAsync();
        var sync = vm.SyncCommand.ExecuteAsync();
        Assert.True(vm.IsOperating);
        var disconnect = vm.DisconnectDeviceCommand.ExecuteAsync();

        Assert.True(token.IsCancellationRequested);
        Assert.False(vm.LogoutCommand.CanExecute(null));
        Assert.False(vm.CreateFolderCommand.CanExecute(null));
        _connection.Verify(c => c.DisconnectAsync(_user, It.IsAny<CancellationToken>()), Times.Never);
        stopped.SetCanceled(token);
        await Task.WhenAll(sync, disconnect);
        _connection.Verify(c => c.DisconnectAsync(_user, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsNotificationError);
        Assert.True(vm.CanManageFiles);
    }

    [Fact]
    public async Task CancelingConfirmationLeavesTheConnectionIntact()
    {
        _dialogs.Setup(d => d.ConfirmDisconnectDevice()).Returns(false);
        using var vm = Create();
        await vm.DisconnectDeviceCommand.ExecuteAsync();
        Assert.True(vm.IsDeviceSyncEnabled);
        _connection.Verify(c => c.DisconnectAsync(_user, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailedDisconnectDisablesSyncButAllowsCloudWorkAndRetry()
    {
        _connection.Setup(c => c.DisconnectAsync(_user, It.IsAny<CancellationToken>())).Returns(() =>
        {
            _enabled = false;
            _state = DeviceConnectionState.DisconnectPending;
            return Task.FromException(new HttpRequestException("Offline"));
        });
        using var vm = Create(new Mock<ISyncEngine>().Object);
        await vm.DisconnectDeviceCommand.ExecuteAsync();
        Assert.True(vm.IsNotificationError);
        Assert.True(vm.CanManageFiles);
        Assert.False(vm.SyncCommand.CanExecute(null));
        Assert.True(vm.DisconnectDeviceCommand.CanExecute(null));
        Assert.Equal("Disconnect pending", vm.SyncStatusSummary);
        Assert.True(vm.ShowSyncSettings);
        Assert.False(vm.ShowSyncSetup);
        Assert.False(vm.ShowSyncAction);
        Assert.Contains("finish disconnecting", vm.SyncStatusText);
    }

    [Fact]
    public async Task ReopeningADisconnectedAccountDoesNotConfigureOrRegisterTheDevice()
    {
        _enabled = false;
        _state = DeviceConnectionState.Disconnected;
        using var vm = Create(new Mock<ISyncEngine>().Object);
        await vm.InitializeAsync();
        Assert.True(vm.CanManageFiles);
        Assert.False(vm.IsDeviceSyncEnabled);
        _api.Verify(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.ConfigureSyncFolderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _settings.Verify(s => s.GetSyncFolderPath(_user), Times.Never);
    }

    [Fact]
    public async Task ReconnectingUsesTheExplicitlyChosenFolder()
    {
        _enabled = false;
        _state = DeviceConnectionState.Disconnected;
        _files.Setup(f => f.SelectFolder(null)).Returns(@"D:\New sync");
        _connection.Setup(c => c.ConnectAsync(_user, @"D:\New sync", It.IsAny<CancellationToken>())).Callback(() =>
        {
            _enabled = true;
            _state = DeviceConnectionState.Connected;
        }).Returns(Task.CompletedTask);
        using var vm = Create(new Mock<ISyncEngine>().Object);
        await vm.InitializeAsync();
        await vm.ChangeSyncFolderCommand.ExecuteAsync();
        Assert.True(vm.IsDeviceSyncEnabled);
        Assert.True(vm.SyncCommand.CanExecute(null));
        _connection.Verify(c => c.ConnectAsync(_user, @"D:\New sync", It.IsAny<CancellationToken>()), Times.Once);
    }
}
