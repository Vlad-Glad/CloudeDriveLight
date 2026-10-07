using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed record ItemMoveRequest(FileItemViewModel Item, FolderDestination Destination);

public sealed partial class MainWindowViewModel
{
    private readonly IFileDialogService _fileDialogs;
    private readonly IUserDialogService _dialogs;
    private CancellationTokenSource? _activeOperation;
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

    public bool IsBusy => IsLoading || IsOperating;
    public bool CanNavigate => !_disposed && !IsOperating;
    public bool CanManageFiles => !_disposed && IsHomeView && !IsBusy && !IsError;
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
    public AsyncRelayCommand<FileItemViewModel> DownloadCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> RenameCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> DeleteCommand { get; private set; } = null!;
    public AsyncRelayCommand<FileItemViewModel> MoveCommand { get; private set; } = null!;
    public AsyncRelayCommand<ItemMoveRequest> MoveToCommand { get; private set; } = null!;
    public RelayCommand DismissNotificationCommand { get; private set; } = null!;
    public RelayCommand CancelOperationCommand { get; private set; } = null!;

    private void InitializeOperationCommands()
    {
        CreateFolderCommand = new AsyncRelayCommand(CreateFolderAsync, HandleOperationError, () => CanManageFiles);
        UploadCommand = new AsyncRelayCommand(UploadAsync, HandleOperationError, () => CanManageFiles);
        DownloadCommand = new AsyncRelayCommand<FileItemViewModel>(DownloadAsync, HandleOperationError,
            file => CanActOn(file) && file?.IsFolder == false);
        RenameCommand = new AsyncRelayCommand<FileItemViewModel>(RenameAsync, HandleOperationError, CanActOn);
        DeleteCommand = new AsyncRelayCommand<FileItemViewModel>(DeleteAsync, HandleOperationError, CanActOn);
        MoveCommand = new AsyncRelayCommand<FileItemViewModel>(MoveAsync, HandleOperationError, CanActOn);
        MoveToCommand = new AsyncRelayCommand<ItemMoveRequest>(MoveItemToAsync, HandleOperationError,
            request => request is not null && CanMoveTo(request.Item, request.Destination));
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

        var parentId = Breadcrumbs.Last().Id;
        await RunOperationAsync("Creating folder...", async token =>
        {
            await _apiService.CreateFolderAsync(name, parentId, token);
            return $"Created '{name}'.";
        });
    }

    private async Task UploadAsync()
    {
        var paths = _fileDialogs.SelectFilesToUpload();
        if (paths.Count == 0)
        {
            return;
        }

        var folderId = Breadcrumbs.Last().Id;
        await RunOperationAsync("Uploading files...", async token =>
        {
            var uploaded = 0;
            var failures = new List<string>();
            foreach (var path in paths)
            {
                token.ThrowIfCancellationRequested();
                OperationStatus = $"Uploading {uploaded + failures.Count + 1} of {paths.Count}...";
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
                : $"Uploaded {uploaded} of {paths.Count} file(s).\n" + string.Join("\n", failures.Take(5));
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

        var name = _dialogs.RequestName("Rename", "New name", item.Name);
        if (name is null || name == item.Name)
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
            await _apiService.MoveAsync(request.Item.Id, request.Item.IsFolder, request.Destination.Id, token);
            return $"Moved '{request.Item.Name}' to '{request.Destination.Name}'.";
        });
    }

    private async Task RunOperationAsync(string status, Func<CancellationToken, Task<string>> operation, bool refreshAfter = true)
    {
        if (_disposed)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _activeOperation = cancellation;
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
        }
        finally
        {
            _activeOperation = null;
            OperationStatus = string.Empty;
            IsOperating = false;
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
}
