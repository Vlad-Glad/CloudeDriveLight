using System.IO;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Services.Api;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Services.Sync;

public sealed class DeviceSyncConnection(
    IDriveApiService api, ILocalSyncDbContextFactory databases, ISyncSettingsService settings,
    string? stateDirectory = null) : IDeviceSyncConnection
{
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private readonly string _directory = stateDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GDriveLight");
    private string PendingPath => Path.Combine(_directory, "device_disconnect_pending.txt");
    private string SetupPath => Path.Combine(_directory, "device_requires_sync_setup.txt");
    private string StatePath(Guid userId) => Path.Combine(_directory, $"device_sync_{userId}.txt");

    public DeviceConnectionState GetState(Guid userId)
    {
        if (File.Exists(PendingPath) && File.ReadAllText(PendingPath).Trim() == userId.ToString())
            return DeviceConnectionState.DisconnectPending;
        var path = StatePath(userId);
        if (!File.Exists(path)) return DeviceConnectionState.Connected;
        return Enum.TryParse<DeviceConnectionState>(File.ReadAllText(path).Trim(), out var state) && Enum.IsDefined(state)
            ? state : throw new DeviceConnectionException("The device connection state is invalid. Restore its settings before synchronizing.");
    }

    public bool IsSyncEnabled(Guid userId) => GetState(userId) == DeviceConnectionState.Connected && !File.Exists(SetupPath);

    public Task InitializeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        TransitionAsync(() => InitializeCoreAsync(userId, cancellationToken), cancellationToken);

    private async Task InitializeCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        VerifySession(userId);
        if (File.Exists(PendingPath))
        {
            if (File.ReadAllText(PendingPath).Trim() != userId.ToString())
                throw new DeviceConnectionException("Sign in to the previous account to finish disconnecting this device first.");
            await DisconnectCoreAsync(userId, cancellationToken);
        }
        else if (GetState(userId) == DeviceConnectionState.DisconnectPending)
            await DisconnectCoreAsync(userId, cancellationToken);

        if (GetState(userId) == DeviceConnectionState.Connected)
        {
            await api.EnsureDeviceRegisteredAsync(cancellationToken);
            if (IsSyncEnabled(userId))
                await api.ConfigureSyncFolderAsync(settings.GetSyncFolderPath(userId), cancellationToken);
        }
    }

    public Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default) =>
        TransitionAsync(() => DisconnectCoreAsync(userId, cancellationToken), cancellationToken);

    private async Task DisconnectCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        VerifySession(userId);
        VerifyPendingOwner(userId);
        Save(StatePath(userId), DeviceConnectionState.DisconnectPending.ToString());
        Save(PendingPath, userId.ToString());
        Save(SetupPath, "Choose a folder explicitly before synchronizing again.");

        await api.DisconnectDeviceAsync(cancellationToken);
        // Never clear local metadata before the server confirms that the files are preserved in Home.
        await using var db = databases.CreateDbContext(userId);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Items.ExecuteDeleteAsync(cancellationToken);
            await db.Cursors.ExecuteDeleteAsync(cancellationToken);
            await db.Scopes.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        settings.ClearSyncFolderPath(userId);
        Save(StatePath(userId), DeviceConnectionState.Disconnected.ToString());
        File.Delete(PendingPath);
    }

    public Task ConnectAsync(Guid userId, string localPath, CancellationToken cancellationToken = default) =>
        TransitionAsync(() => ConnectCoreAsync(userId, localPath, cancellationToken), cancellationToken);

    private async Task ConnectCoreAsync(Guid userId, string localPath, CancellationToken cancellationToken)
    {
        VerifySession(userId);
        VerifyPendingOwner(userId);
        if (File.Exists(PendingPath) || GetState(userId) == DeviceConnectionState.DisconnectPending)
            await DisconnectCoreAsync(userId, cancellationToken);
        await api.EnsureDeviceRegisteredAsync(cancellationToken);
        await api.ConfigureSyncFolderAsync(localPath, cancellationToken);
        settings.SetSyncFolderPath(userId, localPath);
        Save(StatePath(userId), DeviceConnectionState.Connected.ToString());
        File.Delete(SetupPath);
    }

    private async Task TransitionAsync(Func<Task> transition, CancellationToken cancellationToken)
    {
        await _transitionGate.WaitAsync(cancellationToken);
        try { await transition(); }
        finally { _transitionGate.Release(); }
    }

    private void VerifySession(Guid userId)
    {
        if (api.CurrentUserId != userId)
            throw new DeviceConnectionException("The account session has changed. Sign in again before changing device synchronization.");
    }

    private void VerifyPendingOwner(Guid userId)
    {
        if (File.Exists(PendingPath) && File.ReadAllText(PendingPath).Trim() != userId.ToString())
            throw new DeviceConnectionException("Sign in to the previous account to finish disconnecting this device first.");
    }

    private void Save(string path, string value)
    {
        Directory.CreateDirectory(_directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, value);
        File.Move(temporary, path, overwrite: true);
    }
}
