using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;

namespace GDriveLight.Desktop.ViewModels;

public sealed record ItemMoveRequest(FileItemViewModel Item, FolderDestination Destination);
public sealed record FilesMoveRequest(IReadOnlyList<FileItemViewModel> Items, FolderDestination Destination);
public sealed record FileUploadRequest(IReadOnlyList<string> Paths, FolderDestination Destination);

public sealed partial class MainWindowViewModel
{
    private readonly IFileDialogService _fileDialogs;
    private readonly IUserDialogService _dialogs;
    private CancellationTokenSource? _activeOperation;
    private TaskCompletionSource? _operationCompletion;
    private bool _isOperating;
    private string _operationStatus = string.Empty;
    private string _notificationMessage = string.Empty;
    private bool _isNotificationError;

    public bool IsOperating
    {
        get => _isOperating;
        private set
        {
            if (SetProperty(ref _isOperating, value))
            {
                NotifyState();
            }
        }
    }

    public bool IsBusy => IsLoading || IsOperating || _isDisconnecting;
    public bool CanNavigate => !_disposed && !IsOperating && !_isDisconnecting;
    public bool CanManageFiles => !_disposed && !IsBusy && !IsError && (IsHomeView || HasSyncRoot);
    public string OperationStatus { get => _operationStatus; private set => SetProperty(ref _operationStatus, value); }
    public bool IsNotificationError { get => _isNotificationError; private set => SetProperty(ref _isNotificationError, value); }
    public string NotificationMessage
    {
        get => _notificationMessage;
        private set
        {
            if (SetProperty(ref _notificationMessage, value))
            {
                OnPropertyChanged(nameof(HasNotification));
            }
        }
    }

    public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationMessage);
    public AsyncRelayCommand CreateFolderCommand { get; private set; } = null!;
    public AsyncRelayCommand UploadCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileUploadRequest> UploadFilesCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> DownloadCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> RenameCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> DeleteCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> MoveCommand { get; private set; } = null!;
    public AsyncRelayCommand<ItemMoveRequest> MoveToCommand { get; private set; } = null!;
    public AsyncRelayCommand<FilesMoveRequest> MoveFilesToCommand { get; private set; } = null!;
    public AsyncRelayCommand SyncCommand { get; private set; } = null!;
    public AsyncRelayCommand ChangeSyncFolderCommand { get; private set; } = null!;
    public RelayCommand OpenSyncFolderInExplorerCommand { get; private set; } = null!;
    public RelayCommand OpenDatabaseLocationCommand { get; private set; } = null!;
    public RelayCommand DismissNotificationCommand { get; private set; } = null!;
    public RelayCommand CancelOperationCommand { get; private set; } = null!;

    private string _syncStatusSummary = "Not synced in this session";
    public string SyncStatusSummary
    {
        get => _syncStatusSummary;
        private set
        {
            if (SetProperty(ref _syncStatusSummary, value)) NotifySyncPresentation();
        }
    }

    private string _lastSyncTimeText = "Not synced yet";
    public string LastSyncTimeText
    {
        get => _lastSyncTimeText;
        private set => SetProperty(ref _lastSyncTimeText, value);
    }

    public string CurrentSyncFolderPath
    {
        get
        {
            try
            {
                if (!IsSyncedFoldersView && !IsDeviceSyncEnabled) return string.Empty;
                return IsSyncedFoldersView ? SelectedDevice?.LocalPath ?? string.Empty : _apiService.CurrentUserId.HasValue && _syncSettings is not null
                    ? _syncSettings.GetSyncFolderPath(_apiService.CurrentUserId.Value)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private void InitializeOperationCommands()
    {
        CreateFolderCommand = new AsyncRelayCommand(CreateFolderAsync, HandleOperationError, () => CanManageFiles);
        UploadCommand = new AsyncRelayCommand(UploadAsync, HandleOperationError, () => CanManageFiles);
        UploadFilesCommand = new AsyncRelayCommand<FileUploadRequest>(UploadFilesAsync, HandleOperationError,
            request => CanManageFiles && request is { Paths.Count: > 0 });
        DownloadCommand = new AsyncRelayCommand<FileItemViewModel>(DownloadAsync, HandleOperationError,
            file => CanActOn(file) && file?.IsFolder == false);
        RenameCommand = new AsyncRelayCommand<FileItemViewModel>(RenameAsync, HandleOperationError, CanActOn);
        DeleteCommand = new AsyncRelayCommand<FileItemViewModel>(DeleteAsync, HandleOperationError, CanActOn);
        MoveCommand = new AsyncRelayCommand<FileItemViewModel>(MoveAsync, HandleOperationError, CanActOn);
        MoveToCommand = new AsyncRelayCommand<ItemMoveRequest>(MoveItemToAsync, HandleOperationError,
            request => request is not null && CanMoveTo(request.Item, request.Destination));
        MoveFilesToCommand = new AsyncRelayCommand<FilesMoveRequest>(MoveFilesToAsync, HandleOperationError,
            request => request is { Items.Count: > 0 } && request.Items.All(item => !item.IsFolder && CanMoveTo(item, request.Destination)));
        SyncCommand = new AsyncRelayCommand(SyncAsync, HandleOperationError, () => !_disposed && !IsBusy && _syncEngine is not null && IsDeviceSyncEnabled &&
            (IsHomeView || IsCurrentDevice) && !string.IsNullOrWhiteSpace(CurrentSyncFolderPath));
        ChangeSyncFolderCommand = new AsyncRelayCommand(ChangeSyncFolderAsync, HandleOperationError, () => !_disposed && !IsBusy && _syncSettings is not null && (IsHomeView || IsCurrentDevice || !IsDeviceSyncEnabled));
        OpenSyncFolderInExplorerCommand = new RelayCommand(OpenSyncFolderInExplorer, () => !_disposed && !IsBusy && (IsHomeView || IsCurrentDevice) && !string.IsNullOrWhiteSpace(CurrentSyncFolderPath));
        OpenDatabaseLocationCommand = new RelayCommand(OpenDatabaseLocation, () => !_disposed && _apiService.CurrentUserId.HasValue);
        DismissNotificationCommand = new RelayCommand(() => NotificationMessage = string.Empty);
        CancelOperationCommand = new RelayCommand(() => _activeOperation?.Cancel(), () => IsOperating);
    }

    private bool CanActOn(FileItemViewModel? item) => CanManageFiles && item is not null && Files.Contains(item);

    private async Task CreateFolderAsync()
    {
        var name = _dialogs.RequestName("New folder", "Folder name");
        if (name is null)
        {
            return;
        }

        await RunOperationAsync("Creating folder...", async token =>
        {
            var parentId = await GetCurrentFolderIdAsync(token);
            await _apiService.CreateFolderAsync(name, parentId, token);

            return $"Created '{name}'.";
        });
    }

    private async Task UploadAsync()
    {
        var paths = _fileDialogs.SelectFilesToUpload();
        await UploadFilesAsync(new FileUploadRequest(paths, new FolderDestination(Breadcrumbs.Last().Id, CurrentFolderName)));
    }

    private async Task UploadFilesAsync(FileUploadRequest? request)
    {
        if (!CanManageFiles || request is null || request.Paths.Count == 0)
        {
            return;
        }

        var paths = request.Paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var folderId = request.Destination.Id;
        await RunOperationAsync("Uploading files...", async token =>
        {
            var uploaded = 0;
            var failures = new List<string>();

            foreach (var path in paths)
            {
                token.ThrowIfCancellationRequested();
                OperationStatus = $"Uploading {uploaded + failures.Count + 1} of {paths.Length}...";
                try
                {
                    await _apiService.UploadFileAsync(path, folderId, token);
                    uploaded++;

                }
                catch (Exception exception) when (exception is not OperationCanceledException &&
                    exception is not DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
                {
                    failures.Add($"{Path.GetFileName(path)}: {GetOperationError(exception)}");
                }
            }

            IsNotificationError = failures.Count > 0;
            return failures.Count == 0
                ? $"Uploaded {uploaded} file(s)."
                : $"Uploaded {uploaded} of {paths.Length} file(s).\n" + string.Join("\n", failures.Take(5));
        });
    }

    private async Task DownloadAsync(FileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var destination = _fileDialogs.SelectDownloadDestination(item.Name);
        if (destination is null)
        {
            return;
        }

        await RunOperationAsync("Downloading file...", async token =>
        {
            await _apiService.DownloadFileAsync(item.Id, destination, token);
            return $"Saved '{item.Name}'.";
        }, refreshAfter: false);
    }

    private async Task RenameAsync(FileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var originalExtension = item.IsFolder ? string.Empty : Path.GetExtension(item.Name);
        var initialName = item.IsFolder ? item.Name : Path.GetFileNameWithoutExtension(item.Name);
        var name = _dialogs.RequestName("Rename", "New name", initialName);
        if (name is null)
        {
            return;
        }

        // Preserve the original extension if the user did not provide one.
        if (!item.IsFolder && !string.IsNullOrEmpty(originalExtension) &&
            !Path.HasExtension(name))
        {
            name = name + originalExtension;
        }

        if (name == item.Name)
        {
            return;
        }

        await RunOperationAsync("Renaming...", async token =>
        {
            await _apiService.RenameAsync(item.Id, item.IsFolder, name, token);

            return $"Renamed to '{name}'.";
        });
    }

    private async Task DeleteAsync(FileItemViewModel? item)
    {
        if (item is null || !_dialogs.ConfirmDelete(item.Name, item.IsFolder))
        {
            return;
        }

        await RunOperationAsync("Deleting...", async token =>
        {
            await _apiService.DeleteAsync(item.Id, item.IsFolder, token);

            return $"Deleted '{item.Name}'.";
        });
    }

    private async Task MoveAsync(FileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var destination = _dialogs.SelectDestination(item.FolderId, item.IsFolder ? item.Id : null);
        if (destination is not null)
        {
            await MoveItemToAsync(new ItemMoveRequest(item, destination));
        }
    }

    public bool CanMoveTo(FileItemViewModel item, FolderDestination destination) =>
        CanActOn(item) && destination.Id != item.FolderId &&
        (!item.IsFolder || destination.Id != item.Id);

    private async Task MoveItemToAsync(ItemMoveRequest? request)
    {
        if (request is null || !CanMoveTo(request.Item, request.Destination))
        {
            return;
        }

        await RunOperationAsync("Moving...", async token =>
        {
            if (IsSyncedFoldersView && !await IsWithinSelectedSyncTreeAsync(request.Destination.Id, token))
            {
                await CopyToCloudAsync(request.Item.Id, request.Item.Name, request.Item.IsFolder, request.Destination.Id, token);
                return $"Copied '{request.Item.Name}' to '{request.Destination.Name}'.";
            }
            await _apiService.MoveAsync(request.Item.Id, request.Item.IsFolder, request.Destination.Id, token);
            return $"Moved '{request.Item.Name}' to '{request.Destination.Name}'.";
        });
    }

    private async Task MoveFilesToAsync(FilesMoveRequest? request)
    {
        if (request is null) return;
        var items = request.Items.DistinctBy(item => item.Id).ToArray();
        await RunOperationAsync("Transferring files...", async token =>
        {
            var copy = IsSyncedFoldersView && !await IsWithinSelectedSyncTreeAsync(request.Destination.Id, token);
            var transferred = 0;
            var failures = new List<string>();
            foreach (var item in items)
            {
                token.ThrowIfCancellationRequested();
                OperationStatus = $"Transferring {transferred + failures.Count + 1} of {items.Length}...";
                try
                {
                    if (copy) await CopyToCloudAsync(item.Id, item.Name, false, request.Destination.Id, token);
                    else await _apiService.MoveAsync(item.Id, false, request.Destination.Id, token);
                    transferred++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException &&
                    exception is not DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
                {
                    failures.Add($"{item.Name}: {GetOperationError(exception)}");
                }
            }
            IsNotificationError = failures.Count > 0;
            var action = copy ? "Copied" : "Moved";
            return failures.Count == 0 ? $"{action} {transferred} file(s) to '{request.Destination.Name}'."
                : $"{action} {transferred} of {items.Length} file(s).\n" + string.Join("\n", failures.Take(5));
        });
    }

    private async Task<bool> IsWithinSelectedSyncTreeAsync(Guid? folderId, CancellationToken token)
    {
        if (!folderId.HasValue) return false;
        if (folderId == SelectedDevice?.SyncRootFolderId) return true;
        return (await _apiService.GetFolderPathAsync(folderId.Value, token)).Any(folder => folder.Id == SelectedDevice?.SyncRootFolderId);
    }

    private async Task CopyToCloudAsync(Guid id, string name, bool isFolder, Guid? destination, CancellationToken token)
    {
        if (isFolder)
        {
            var targetId = await _apiService.CreateFolderAsync(name, destination, token);
            foreach (var folder in await _apiService.GetFolderContentAsync(id, token))
                await CopyToCloudAsync(folder.Id, folder.Name, true, targetId, token);
            foreach (var file in await _apiService.GetFilesAsync(id, token))
                await CopyToCloudAsync(file.Id, file.Name, false, targetId, token);
            return;
        }
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"gdrivelight-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var temporaryFile = Path.Combine(temporaryDirectory, Path.GetFileName(name));
        try
        {
            await _apiService.DownloadFileAsync(id, temporaryFile, token);
            await _apiService.UploadFileAsync(temporaryFile, destination, token);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
            Directory.Delete(temporaryDirectory);
        }
    }

    private async Task SyncAsync()
    {
        if (_syncEngine is null || !IsDeviceSyncEnabled)
        {
            return;
        }

        var userId = _apiService.CurrentUserId;
        if (!userId.HasValue)
        {
            NotificationMessage = "You must be logged in to synchronize.";
            IsNotificationError = true;
            return;
        }

        var folder = _syncSettings?.GetSyncFolderPath(userId.Value);
        if (string.IsNullOrWhiteSpace(folder))
        {
            NotificationMessage = "Sync folder is not configured.";
            IsNotificationError = true;
            return;
        }

        _isSynchronizing = true;
        _hasSyncIssue = false;
        SyncStatusSummary = "Syncing this device…";
        NotifySyncPresentation();
        try
        {
            await RunOperationAsync("Synchronizing this device…", async token =>
            {
                try
                {
                    var config = await _apiService.ConfigureSyncFolderAsync(folder, token);
                    var result = await _syncEngine.SynchronizeAsync(userId.Value, folder, token, config.SyncRootFolderId);
                    token.ThrowIfCancellationRequested();
                    if (result.Success && result.FailedCount == 0)
                    {
                        _lastSuccessfulSyncAt = DateTime.Now;
                        LastSyncTimeText = _lastSuccessfulSyncAt.Value.ToString("g");
                        SyncStatusSummary = $"Last sync completed: {LastSyncTimeText}";
                        return result.Message ?? $"Synchronized {result.ProcessedChangesCount} items.";
                    }

                    _hasSyncIssue = true;
                    SyncStatusSummary = result.FailedCount > 0 ? "Some files could not sync — try again" : "Sync failed — try again";
                    IsNotificationError = true;
                    return result.Message ?? "Synchronization completed with errors.";
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    SyncStatusSummary = "Sync cancelled — run it again to finish";
                    throw;
                }
                catch
                {
                    _hasSyncIssue = true;
                    SyncStatusSummary = "Sync failed — try again";
                    throw;
                }
            }, refreshAfter: true);
        }
        finally
        {
            _isSynchronizing = false;
            NotifySyncPresentation();
        }
    }

    private async Task ChangeSyncFolderAsync()
    {
        var userId = _apiService.CurrentUserId;
        if (!userId.HasValue)
        {
            NotificationMessage = "You must be logged in to configure the sync folder.";
            IsNotificationError = true;
            return;
        }

        var current = IsDeviceSyncEnabled ? _syncSettings?.GetSyncFolderPath(userId.Value) : null;
        var selected = _fileDialogs.SelectFolder(current);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            await RunOperationAsync("Changing sync folder...", async token =>
            {
                if (_deviceConnection is not null)
                {
                    await _deviceConnection.ConnectAsync(userId.Value, Path.GetFullPath(selected), token);
                    UpdateConnectionState(userId.Value);
                }
                else
                {
                    await _apiService.ConfigureSyncFolderAsync(selected, token);
                    _syncSettings!.SetSyncFolderPath(userId.Value, Path.GetFullPath(selected));
                }
                OnPropertyChanged(nameof(CurrentSyncFolderPath));
                return $"Sync folder changed to '{selected}'. Click Sync this device to exchange files with the cloud.";
            });
        }
    }

    private void OpenSyncFolderInExplorer()
    {
        var syncFolder = CurrentSyncFolderPath;
        if (string.IsNullOrWhiteSpace(syncFolder) || !Directory.Exists(syncFolder))
        {
            return;
        }

        var relativeParts = Breadcrumbs.Skip(1).Select(b => b.Name).ToArray();
        var targetDir = (IsSyncedFoldersView && relativeParts.Length > 0)
            ? Path.Combine(syncFolder, Path.Combine(relativeParts))
            : syncFolder;

        if (!Directory.Exists(targetDir))
        {
            targetDir = syncFolder;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/n, \"{Path.GetFullPath(targetDir)}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            NotificationMessage = $"Could not open folder: {ex.Message}";
            IsNotificationError = true;
        }
    }

    private void OpenDatabaseLocation()
    {
        var userId = _apiService.CurrentUserId;
        if (!userId.HasValue)
        {
            NotificationMessage = "You must be logged in to inspect the database.";
            IsNotificationError = true;
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(localAppData, "GDriveLight", $"sync_{userId.Value}.db");
        var dir = Path.GetDirectoryName(dbPath);

        if (File.Exists(dbPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{dbPath}\"",
                    UseShellExecute = true
                });

                NotificationMessage = $"Database selected in Explorer: sync_{userId.Value}.db";
                IsNotificationError = false;
            }
            catch (Exception ex)
            {
                NotificationMessage = $"Could not open database location: {ex.Message}";
                IsNotificationError = true;
            }
        }
        else if (Directory.Exists(dir))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
            NotificationMessage = "Database directory opened in Explorer.";
            IsNotificationError = false;
        }
        else
        {
            NotificationMessage = "Database file does not exist yet. Run synchronization first.";
            IsNotificationError = true;
        }
    }

    private async Task RunOperationAsync(string status, Func<CancellationToken, Task<string>> operation, bool refreshAfter = true)
    {
        if (_disposed)
        {
            return;
        }

        CancelLoad();
        _searchDebounce?.Cancel();
        IsLoading = false;
        using var cancellation = new CancellationTokenSource();
        _activeOperation = cancellation;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _operationCompletion = completion;
        IsOperating = true;
        OperationStatus = status;
        NotificationMessage = string.Empty;
        IsNotificationError = false;
        try
        {
            var message = await operation(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed)
            {
                return;
            }

            NotificationMessage = message;
            if (refreshAfter)
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            IsNotificationError = true;
            NotificationMessage = "Operation cancelled. Refresh to check the result before trying again.";
            if (refreshAfter && !_disposed)
            {
                await RefreshAsync();
            }
        }
        catch (Exception exception)
        {
            HandleOperationError(exception);
            if (refreshAfter && !_disposed && exception is not DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
                _ = StorageUsage.RefreshAsync();
        }
        finally
        {
            _activeOperation = null;
            OperationStatus = string.Empty;
            IsOperating = false;
            _operationCompletion = null;
            completion.TrySetResult();
        }
    }

    private void HandleOperationError(Exception exception)
    {
        if (_disposed)
        {
            return;
        }

        if (exception is DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
        {
            Logout();
            return;
        }

        IsNotificationError = true;
        NotificationMessage = GetOperationError(exception);
    }

    private static string GetOperationError(Exception exception) => exception switch
    {
        DeviceConnectionException => exception.Message,
        DriveApiException => exception.Message,
        OperationCanceledException => "The request timed out. Check the result before trying again.",
        HttpRequestException => "Could not complete the request. Check the connection and refresh before trying again.",
        UnauthorizedAccessException => "Access to the local file was denied.",
        FileNotFoundException => "The local file could not be found.",
        IOException => "The local file could not be read or saved. Check that it is not in use.",
        JsonException => "The server returned an invalid response. Refresh before trying again.",
        ArgumentException => "Check the file name and ensure the file is not empty.",
        _ => "The operation could not be completed. Refresh before trying again."
    };

    private Task<Guid?> GetCurrentFolderIdAsync(CancellationToken token)
        => Task.FromResult(Breadcrumbs.Last().Id);
}
