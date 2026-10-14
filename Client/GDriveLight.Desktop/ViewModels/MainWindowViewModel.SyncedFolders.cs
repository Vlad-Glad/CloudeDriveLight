using System.Collections.ObjectModel;
using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DeviceItemViewModel? _selectedDevice;
    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();
    public DeviceItemViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!CanNavigate || value is null || !Devices.Contains(value) || !SetProperty(ref _selectedDevice, value)) return;
            if (IsSyncedFoldersView)
            {
                ResetBrowsingRoot();
                _ = RefreshAsync();
            }
            NotifyDeviceState();
        }
    }

    public bool IsCurrentDevice => SelectedDevice?.IsCurrentDevice == true;
    public bool HasSyncRoot => SelectedDevice?.SyncRootFolderId.HasValue == true;
    public string SyncFolderHint => IsCurrentDevice
        ? "Cloud changes are applied to this local folder when you synchronize."
        : "Local folder on the selected device. Change its path on that device.";

    private void ResetBrowsingRoot()
    {
        CancelLoad();
        _searchDebounce?.Cancel();
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem(IsSyncedFoldersView ? SelectedDevice?.SyncRootFolderId : null,
            IsSyncedFoldersView ? SelectedDevice?.Name ?? "Synced folders" : "Home"));
        Files.Clear();
        VisibleFiles.Clear();
        SelectedFile = null;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        IsError = false;
        IsLoading = false;
        NotifyState();
    }

    private void NotifyDeviceState()
    {
        OnPropertyChanged(nameof(SelectedDevice));
        OnPropertyChanged(nameof(IsCurrentDevice));
        OnPropertyChanged(nameof(HasSyncRoot));
        OnPropertyChanged(nameof(CurrentSyncFolderPath));
        OnPropertyChanged(nameof(SyncFolderHint));
        ChangeSyncFolderCommand.NotifyCanExecuteChanged();
        OpenSyncFolderInExplorerCommand.NotifyCanExecuteChanged();
        NotifyState();
    }

    private async Task LoadDevicesAsync(CancellationToken token, long version)
    {
        var responses = await _apiService.GetDevicesAsync(token);
        if (_disposed || version != _loadVersion) return;
        var selectedId = SelectedDevice?.Id ?? _apiService.DeviceId;
        Devices.Clear();
        foreach (var device in responses)
            Devices.Add(new DeviceItemViewModel(device.Id, device.Name, device.SyncRootFolderId, device.LocalPath, device.Id == _apiService.DeviceId));
        _selectedDevice = Devices.FirstOrDefault(d => d.Id == selectedId) ?? Devices.FirstOrDefault();
        if (Breadcrumbs.Count == 1)
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem(SelectedDevice?.SyncRootFolderId, SelectedDevice?.Name ?? "Synced folders"));
        }
        NotifyDeviceState();
    }

    private async Task<(IEnumerable<FolderResponse> Folders, IEnumerable<FileResponse> Files)> SearchSyncedTreeAsync(string query, CancellationToken token)
    {
        var matchingFolders = new List<FolderResponse>();
        var matchingFiles = new List<FileResponse>();
        var queue = new Queue<Guid>();
        var visited = new HashSet<Guid>();
        queue.Enqueue(SelectedDevice!.SyncRootFolderId!.Value);
        while (queue.TryDequeue(out var folderId))
        {
            if (!visited.Add(folderId)) continue;
            var foldersTask = _apiService.GetFolderContentAsync(folderId, token);
            var filesTask = _apiService.GetFilesAsync(folderId, token);
            await Task.WhenAll(foldersTask, filesTask);
            foreach (var folder in await foldersTask)
            {
                queue.Enqueue(folder.Id);
                if (folder.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)) matchingFolders.Add(folder);
            }
            matchingFiles.AddRange((await filesTask).Where(file => file.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        }
        return (matchingFolders, matchingFiles);
    }
}
