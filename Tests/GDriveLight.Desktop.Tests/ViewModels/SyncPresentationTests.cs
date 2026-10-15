using System.Net.Http;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class SyncPresentationTests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();
    private readonly Guid _root = Guid.NewGuid();
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly Mock<ISyncSettingsService> _settings = new(MockBehavior.Strict);
    private readonly Mock<ISyncEngine> _engine = new(MockBehavior.Strict);

    public SyncPresentationTests()
    {
        _api.SetupGet(service => service.CurrentUserId).Returns(_user);
        _api.SetupGet(service => service.DeviceId).Returns(_device);
        _api.Setup(service => service.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new DeviceResponse(_device, "This computer", _root, @"C:\Sync")
        });
        _api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_device);
        _api.Setup(service => service.ConfigureSyncFolderAsync(@"C:\Sync", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceResponse(_device, "This computer", _root, @"C:\Sync"));
        _api.Setup(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StorageUsageResponse(100, 0, 100, false));
        _api.Setup(service => service.GetFolderContentAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        _api.Setup(service => service.GetFilesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        _settings.Setup(service => service.GetSyncFolderPath(_user)).Returns(@"C:\Sync");
    }

    private MainWindowViewModel Create() => new(_api.Object, new Mock<IFileDialogService>().Object,
        new Mock<IUserDialogService>().Object, syncEngine: _engine.Object, syncSettings: _settings.Object);

    [Fact]
    public async Task InitialStateExplainsManualSyncAndRefreshDoesNotRunIt()
    {
        using var model = Create();
        await model.InitializeAsync();
        await model.RetryCommand.ExecuteAsync();
        Assert.Equal("Not synced in this session", model.SyncStatusText);
        Assert.Contains("only when you click", model.SyncHelpText);
        Assert.Contains("Refresh only reloads cloud files", model.SyncHelpText);
        Assert.False(model.ShowSyncAction);
        Assert.False(model.ShowSyncSetup);
        Assert.True(model.SyncCommand.CanExecute(null));
        model.SelectedSidebarIndex = 1;
        await model.RefreshAsync();
        Assert.True(model.ShowSyncAction);
        Assert.Equal("Not synced yet", model.SyncHeaderStatusText);
        model.SelectedSidebarIndex = 0;
        Assert.False(model.ShowSyncAction);
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunningAndCompletedStatesAreExplicitAndOnlySuccessUpdatesTheTime()
    {
        var completion = new TaskCompletionSource<SyncResult>();
        _engine.Setup(service => service.SynchronizeAsync(_user, @"C:\Sync", It.IsAny<CancellationToken>(), _root))
            .Returns(completion.Task);
        using var model = Create();
        var syncing = model.SyncCommand.ExecuteAsync();
        Assert.True(model.IsSynchronizing);
        Assert.Equal("Syncing…", model.SyncActionLabel);
        Assert.Contains("Syncing this device", model.SyncStatusText);
        Assert.False(model.SyncCommand.CanExecute(null));
        Assert.False(model.RetryCommand.CanExecute(null));
        completion.SetResult(new SyncResult(true, 3));
        await syncing;
        Assert.False(model.IsSynchronizing);
        Assert.False(model.HasSyncIssue);
        Assert.StartsWith("Last sync completed:", model.SyncStatusText);
        Assert.NotEqual("Not synced yet", model.LastSyncTimeText);
        Assert.StartsWith("Last sync:", model.SyncHeaderStatusText);
        Assert.Contains(model.LastSyncTimeText, model.SyncStatusHelp);
        Assert.Equal("Sync this device", model.SyncActionLabel);
        Assert.True(model.SyncCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task FailedOrPartialResultsExplainThatAnotherSyncIsNeeded(bool success, int failed)
    {
        _engine.Setup(service => service.SynchronizeAsync(_user, @"C:\Sync", It.IsAny<CancellationToken>(), _root))
            .ReturnsAsync(new SyncResult(success, 2, FailedCount: failed));
        using var model = Create();
        await model.SyncCommand.ExecuteAsync();
        Assert.False(model.IsSynchronizing);
        Assert.True(model.HasSyncIssue);
        Assert.Contains("try again", model.SyncStatusText);
        Assert.Equal("Not synced yet", model.LastSyncTimeText);
        Assert.True(model.SyncCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkFailureOrTimeoutNeverLeavesAProgressStateOrClaimsSuccess(bool timeout)
    {
        Exception failure = timeout ? new TaskCanceledException("timeout") : new HttpRequestException("offline");
        _engine.Setup(service => service.SynchronizeAsync(_user, @"C:\Sync", It.IsAny<CancellationToken>(), _root))
            .ThrowsAsync(failure);
        using var model = Create();
        await model.SyncCommand.ExecuteAsync();
        Assert.False(model.IsSynchronizing);
        Assert.True(model.HasSyncIssue);
        Assert.Equal("Sync failed — try again", model.SyncStatusText);
        Assert.Equal("Not synced yet", model.LastSyncTimeText);
    }

    [Fact]
    public async Task CancellationIsReportedAsCancellationEvenIfTheEngineReturnsSuccessLate()
    {
        var completion = new TaskCompletionSource<SyncResult>();
        _engine.Setup(service => service.SynchronizeAsync(_user, @"C:\Sync", It.IsAny<CancellationToken>(), _root)).Returns(completion.Task);
        using var model = Create();
        var syncing = model.SyncCommand.ExecuteAsync();
        model.CancelOperationCommand.Execute(null);
        completion.SetResult(new SyncResult(true, 3));
        await syncing;
        Assert.False(model.IsSynchronizing);
        Assert.Contains("cancelled", model.SyncStatusText);
        Assert.Equal("Not synced yet", model.LastSyncTimeText);
    }

    [Fact]
    public async Task OtherDevicesOnlyOfferCloudRefreshAndExplainWhereSyncRuns()
    {
        _api.Setup(service => service.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new DeviceResponse(_device, "This computer", _root, @"C:\Sync"),
            new DeviceResponse(Guid.NewGuid(), "Other computer", Guid.NewGuid(), @"D:\Other")
        });
        using var model = Create();
        model.SelectedSidebarIndex = 1;
        await model.RefreshAsync();
        model.SelectedDevice = model.Devices[1];
        Assert.False(model.ShowSyncAction);
        Assert.False(model.ShowSyncSetup);
        Assert.False(model.SyncCommand.CanExecute(null));
        Assert.Contains("another device", model.SyncStatusText);
        Assert.Contains("Sync runs on that device", model.SyncHelpText);
        Assert.True(model.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingFolderOffersSetupInsteadOfAnUnusableSyncButton()
    {
        _settings.Setup(service => service.GetSyncFolderPath(_user)).Returns(string.Empty);
        using var model = Create();
        await model.RefreshAsync();
        Assert.True(model.ShowSyncSetup);
        Assert.False(model.ShowSyncAction);
        Assert.False(model.SyncCommand.CanExecute(null));
        Assert.Contains("Choose a local folder", model.SyncStatusText);
        Assert.True(model.ChangeSyncFolderCommand.CanExecute(null));
    }
}
