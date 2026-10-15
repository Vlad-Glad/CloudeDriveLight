using GDriveLight.Desktop.Services.Sync;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _isSynchronizing;
    private bool _hasSyncIssue;
    private bool IsViewingOtherDevice => IsSyncedFoldersView && SelectedDevice is not null && !IsCurrentDevice;
    private bool HasLocalSyncFolder => !string.IsNullOrWhiteSpace(CurrentSyncFolderPath);

    public bool IsSynchronizing => _isSynchronizing;
    public bool ShowSyncSettings => !IsViewingOtherDevice && _deviceConnectionState == DeviceConnectionState.DisconnectPending;
    public bool ShowSyncSetup => !IsViewingOtherDevice && !ShowSyncSettings && _syncSettings is not null && (!IsDeviceSyncEnabled || !HasLocalSyncFolder);
    public bool ShowSyncAction => !IsViewingOtherDevice && !ShowSyncSettings && !ShowSyncSetup && IsDeviceSyncEnabled;
    public string SyncActionLabel => IsSynchronizing ? "Syncing…" : "Sync this device";
    public bool HasSyncIssue => !IsViewingOtherDevice &&
        (_deviceConnectionState == DeviceConnectionState.DisconnectPending || IsDeviceSyncEnabled && _hasSyncIssue);
    public string SyncStatusText => IsViewingOtherDevice ? "Viewing another device's cloud files"
        : _isDisconnecting ? "Disconnecting this device…"
        : _deviceConnectionState == DeviceConnectionState.DisconnectPending ? "Sync paused — finish disconnecting in Account settings"
        : !IsDeviceSyncEnabled ? "Sync is off for this device"
        : _syncEngine is null || _syncSettings is null ? "Device sync is unavailable"
        : !HasLocalSyncFolder ? "Choose a local folder to start syncing"
        : SyncStatusSummary;
    public string SyncHelpText => IsViewingOtherDevice
        ? "Sync runs on that device. Refresh list to see its latest uploaded files."
        : _isDisconnecting ? "Waiting for the current operation to stop. Your local files will stay in place."
        : _deviceConnectionState == DeviceConnectionState.DisconnectPending ? "Cloud files are still available. Open Account settings to finish disconnecting."
        : _syncEngine is null || _syncSettings is null ? "Cloud files are available. Local folder sync is not configured in this app."
        : !IsDeviceSyncEnabled || !HasLocalSyncFolder ? "Choose a folder on this computer to exchange changes with its cloud copy."
        : "Sync starts only when you click Sync this device. Refresh list only reloads cloud files.";
    public string SyncActionHelp => $"Exchange changes between this computer's local folder and its cloud copy. Local folder: {CurrentSyncFolderPath}";

    private void NotifySyncPresentation()
    {
        foreach (var property in new[] { nameof(IsSynchronizing), nameof(ShowSyncSetup), nameof(ShowSyncAction), nameof(ShowSyncSettings),
                     nameof(SyncActionLabel), nameof(HasSyncIssue), nameof(SyncStatusText), nameof(SyncHelpText), nameof(SyncActionHelp) })
            OnPropertyChanged(property);
    }
}
