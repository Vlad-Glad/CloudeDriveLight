using System.IO;
using System.Net.Http;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class DeviceSyncConnectionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gdl-detach-{Guid.NewGuid():N}");
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();
    private readonly Mock<IDriveApiService> _api = new(MockBehavior.Strict);
    private readonly LocalSyncDbContextFactory _databases;
    private readonly SyncSettingsService _settings;
    private readonly string _local;

    public DeviceSyncConnectionTests()
    {
        _databases = new LocalSyncDbContextFactory(Path.Combine(_directory, "metadata"));
        _settings = new SyncSettingsService(Path.Combine(_directory, "metadata"));
        _local = Path.Combine(_directory, "local-files");
        Directory.CreateDirectory(_local);
        File.WriteAllText(Path.Combine(_local, "keep.txt"), "Never delete this file");
        _settings.SetSyncFolderPath(_user, _local);
        Seed(_user);
        Seed(_otherUser);
        _api.SetupGet(a => a.CurrentUserId).Returns(_user);
        _api.Setup(a => a.ConfigureSyncFolderAsync(_local, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceResponse(Guid.NewGuid(), "Laptop", Guid.NewGuid(), _local));
        _api.Setup(a => a.DisconnectDeviceAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private DeviceSyncConnection Create(ISyncSettingsService? settings = null) =>
        new(_api.Object, _databases, settings ?? _settings, Path.Combine(_directory, "metadata"));

    private void Seed(Guid user)
    {
        using var db = _databases.CreateDbContext(user);
        db.Items.Add(new LocalItemState { RelativePath = "keep.txt", ServerResourceId = Guid.NewGuid(), Status = SyncStatus.Synced });
        db.Cursors.Add(new SyncCursor { Id = 1, LastProcessedChangeId = 25 });
        db.Scopes.Add(new SyncScope { Id = 1, LocalPath = _local, ServerRootFolderId = Guid.NewGuid(), InitialSyncCompleted = true });
        db.SaveChanges();
    }

    [Fact]
    public async Task DisconnectClearsOnlyCurrentAccountMetadataAndPreservesFilesAndSession()
    {
        var service = Create();
        await service.DisconnectAsync(_user);
        await using var db = _databases.CreateDbContext(_user);
        Assert.Empty(await db.Items.ToListAsync());
        Assert.Empty(await db.Cursors.ToListAsync());
        Assert.Empty(await db.Scopes.ToListAsync());
        await using var other = _databases.CreateDbContext(_otherUser);
        Assert.Single(await other.Items.ToListAsync());
        Assert.Equal("Never delete this file", File.ReadAllText(Path.Combine(_local, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(_directory, "metadata", $"sync_folder_{_user}.txt")));
        Assert.Equal(DeviceConnectionState.Disconnected, service.GetState(_user));
        Assert.False(service.IsSyncEnabled(_user));
        Assert.Equal(_user, _api.Object.CurrentUserId);
        _api.Verify(a => a.Logout(), Times.Never);
    }

    [Fact]
    public async Task FailedServerRequestPreservesMetadataAndCanBeRetriedAfterRestart()
    {
        _api.SetupSequence(a => a.DisconnectDeviceAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Offline")).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<HttpRequestException>(() => Create().DisconnectAsync(_user));
        using (var db = _databases.CreateDbContext(_user)) Assert.Single(db.Items);
        Assert.Equal(_local, _settings.GetSyncFolderPath(_user));
        var restarted = Create();
        Assert.Equal(DeviceConnectionState.DisconnectPending, restarted.GetState(_user));
        Assert.False(restarted.IsSyncEnabled(_user));
        await restarted.InitializeAsync(_user);
        Assert.Equal(DeviceConnectionState.Disconnected, restarted.GetState(_user));
        using (var db = _databases.CreateDbContext(_user)) Assert.Empty(db.Items);
        _api.Verify(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OtherAccountsCannotClaimTheDeviceUntilPendingCleanupFinishes()
    {
        _api.Setup(a => a.DisconnectDeviceAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        await Assert.ThrowsAsync<HttpRequestException>(() => Create().DisconnectAsync(_user));
        _api.SetupGet(a => a.CurrentUserId).Returns(_otherUser);
        await Assert.ThrowsAsync<DeviceConnectionException>(() => Create().InitializeAsync(_otherUser));
        _api.Verify(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LocalCleanupFailureStaysPendingAndIsRecoverable()
    {
        var settings = new Mock<ISyncSettingsService>();
        settings.SetupSequence(s => s.ClearSyncFolderPath(_user)).Throws(new IOException("Locked")).Pass();
        await Assert.ThrowsAsync<IOException>(() => Create(settings.Object).DisconnectAsync(_user));
        Assert.Equal(DeviceConnectionState.DisconnectPending, Create().GetState(_user));
        await Create().InitializeAsync(_user);
        Assert.Equal(DeviceConnectionState.Disconnected, Create().GetState(_user));
        Assert.True(File.Exists(Path.Combine(_local, "keep.txt")));
    }

    [Fact]
    public async Task DisconnectedAccountDoesNotAutomaticallyReclaimTheDevice()
    {
        await Create().DisconnectAsync(_user);
        await Create().InitializeAsync(_user);
        _api.Verify(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Never);
        _api.Verify(a => a.ConfigureSyncFolderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NewAccountMustExplicitlyChooseAFolderBeforeSyncing()
    {
        await Create().DisconnectAsync(_user);
        _api.SetupGet(a => a.CurrentUserId).Returns(_otherUser);
        _api.Setup(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var service = Create();
        await service.InitializeAsync(_otherUser);
        Assert.False(service.IsSyncEnabled(_otherUser));
        _api.Verify(a => a.ConfigureSyncFolderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var selected = Path.Combine(_directory, "new-account-files");
        _api.Setup(a => a.ConfigureSyncFolderAsync(selected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceResponse(Guid.NewGuid(), "Laptop", Guid.NewGuid(), selected));
        await service.ConnectAsync(_otherUser, selected);
        Assert.True(service.IsSyncEnabled(_otherUser));
        Assert.Equal(selected, _settings.GetSyncFolderPath(_otherUser));
        Assert.False(service.IsSyncEnabled(_user));
        Assert.True(File.Exists(Path.Combine(_local, "keep.txt")));
    }

    [Fact]
    public async Task DisconnectWaitsForInFlightRegistrationAndCannotBeUndoneByInitialization()
    {
        var registration = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Setup(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).Returns(registration.Task);
        var service = Create();
        var initialize = service.InitializeAsync(_user);
        var disconnect = service.DisconnectAsync(_user);
        _api.Verify(a => a.DisconnectDeviceAsync(It.IsAny<CancellationToken>()), Times.Never);
        registration.SetResult(Guid.NewGuid());
        await Task.WhenAll(initialize, disconnect);
        await service.InitializeAsync(_user);
        Assert.Equal(DeviceConnectionState.Disconnected, service.GetState(_user));
        _api.Verify(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisconnectAlsoWaitsForInitialFolderConfiguration()
    {
        _api.Setup(a => a.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var configured = new TaskCompletionSource<DeviceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.Setup(a => a.ConfigureSyncFolderAsync(_local, It.IsAny<CancellationToken>())).Returns(configured.Task);
        var service = Create();
        var initialize = service.InitializeAsync(_user);
        var disconnect = service.DisconnectAsync(_user);
        _api.Verify(a => a.DisconnectDeviceAsync(It.IsAny<CancellationToken>()), Times.Never);
        configured.SetResult(new DeviceResponse(Guid.NewGuid(), "Laptop", Guid.NewGuid(), _local));
        await Task.WhenAll(initialize, disconnect);
        Assert.False(service.IsSyncEnabled(_user));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var resolved = Path.GetFullPath(_directory);
        if (Path.GetDirectoryName(resolved) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(resolved).StartsWith("gdl-detach-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
