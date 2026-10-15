using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services.Sync;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly IDeviceSyncConnection? _deviceConnection;
    private readonly CancellationTokenSource _connectionLifetime = new();
    private DeviceConnectionState _deviceConnectionState = DeviceConnectionState.Connected;
    private bool _isDeviceSyncEnabled = true;
    private bool _isDisconnecting;

    public AsyncRelayCommand DisconnectDeviceCommand { get; }
    public bool IsDeviceSyncEnabled => _isDeviceSyncEnabled;
    public string DeviceConnectionStatus => _isDisconnecting ? "Disconnecting..."
        : _deviceConnectionState == DeviceConnectionState.DisconnectPending ? "Disconnect pending — retry to finish"
        : IsDeviceSyncEnabled ? "Synchronization connected" : "Synchronization disconnected";

    private void UpdateConnectionState(Guid userId)
    {
        if (_deviceConnection is null) return;
        _deviceConnectionState = _deviceConnection.GetState(userId);
        _isDeviceSyncEnabled = _deviceConnection.IsSyncEnabled(userId);
        if (!IsDeviceSyncEnabled)
        {
            SyncStatusSummary = _deviceConnectionState == DeviceConnectionState.DisconnectPending ? "Disconnect pending" : "Sync disconnected";
            LastSyncTimeText = string.Empty;
        }
        else if (SyncStatusSummary is "Sync disconnected" or "Disconnect pending")
        {
            _hasSyncIssue = false;
            SyncStatusSummary = "Not synced in this session";
        }
        OnPropertyChanged(nameof(CurrentSyncFolderPath));
        OnPropertyChanged(nameof(IsDeviceSyncEnabled));
        OnPropertyChanged(nameof(DeviceConnectionStatus));
        NotifyState();
    }

    private async Task DisconnectDeviceAsync()
    {
        if (_deviceConnection is null || _apiService.CurrentUserId is not Guid userId || !_dialogs.ConfirmDisconnectDevice()) return;
        _isDisconnecting = true;
        NotifyState();
        OnPropertyChanged(nameof(DeviceConnectionStatus));
        CancelLoad();
        _searchDebounce?.Cancel();
        _activeOperation?.Cancel();
        try
        {
            // Await disposal of the sync DbContext before clearing its metadata.
            await (_operationCompletion?.Task ?? Task.CompletedTask);
            await _deviceConnection.DisconnectAsync(userId, _connectionLifetime.Token);
            if (_disposed) return;
            Devices.Clear();
            _selectedDevice = null;
            _selectedSidebarIndex = 0;
            OnPropertyChanged(nameof(SelectedSidebarIndex));
            OnPropertyChanged(nameof(IsHomeView));
            OnPropertyChanged(nameof(IsSyncedFoldersView));
            ResetBrowsingRoot();
            NotifyDeviceState();
            IsNotificationError = false;
            NotificationMessage = "Device disconnected. Local files are unchanged; the cloud folder is now in Home.";
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) { HandleOperationError(exception); }
        finally
        {
            _isDisconnecting = false;
            if (!_disposed) UpdateConnectionState(userId);
        }
    }
}
