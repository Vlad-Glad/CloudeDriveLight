namespace GDriveLight.Desktop.Services.Sync;

public enum DeviceConnectionState { Connected, DisconnectPending, Disconnected }

public interface IDeviceSyncConnection
{
    DeviceConnectionState GetState(Guid userId);
    bool IsSyncEnabled(Guid userId);
    Task InitializeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ConnectAsync(Guid userId, string localPath, CancellationToken cancellationToken = default);
}

public sealed class DeviceConnectionException(string message) : Exception(message);
