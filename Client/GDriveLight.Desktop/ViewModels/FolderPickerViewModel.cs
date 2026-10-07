using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed class FolderPickerViewModel : ViewModelBase, IDisposable
{
    private readonly IDriveApiService _apiService;
    private readonly Guid? _currentFolderId;
    private readonly Guid? _excludedFolderId;
    private CancellationTokenSource? _activeLoad;
    private long _loadVersion;
    private bool _disposed;
    private bool _ready;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private FolderResponse? _selectedFolder;

    public FolderPickerViewModel(IDriveApiService apiService, Guid? currentFolderId, Guid? excludedFolderId)
    {
        _apiService = apiService;
        _currentFolderId = currentFolderId;
        _excludedFolderId = excludedFolderId;
        Breadcrumbs.Add(new BreadcrumbItem(null, "Home"));
        OpenFolderCommand = new AsyncRelayCommand<FolderResponse>(OpenFolderAsync, HandleError,
            folder => CanBrowse && _ready && folder is not null && Folders.Contains(folder));
        NavigateBackCommand = new AsyncRelayCommand<BreadcrumbItem>(NavigateBackAsync, HandleError,
            crumb => CanBrowse && crumb is not null && Breadcrumbs.Contains(crumb) && crumb != Breadcrumbs.Last());
        RetryCommand = new AsyncRelayCommand(InitializeAsync, HandleError, () => CanBrowse);
        ConfirmCommand = new RelayCommand(Confirm, () => CanConfirm);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
    public IEnumerable<BreadcrumbItem> BreadcrumbTrail => Breadcrumbs.Take(Math.Max(0, Breadcrumbs.Count - 1));
    public string CurrentFolderName => Breadcrumbs.Last().Name;
    public ObservableCollection<FolderResponse> Folders { get; } = new();
    public FolderResponse? SelectedFolder { get => _selectedFolder; set => SetProperty(ref _selectedFolder, value); }
    public bool IsLoading => _isLoading;
    public string ErrorMessage => _errorMessage;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool CanBrowse => !_disposed && !IsLoading;
    public bool CanConfirm => CanBrowse && _ready && !HasError && Breadcrumbs.Last().Id != _currentFolderId &&
        (!_excludedFolderId.HasValue || Breadcrumbs.Last().Id != _excludedFolderId);
    public bool IsContentVisible => CanBrowse && _ready && !HasError && Folders.Count > 0;
    public bool IsEmpty => CanBrowse && _ready && !HasError && Folders.Count == 0;
    public string LocationHint => Breadcrumbs.Last().Id == _currentFolderId
        ? "This is the current location. Choose another folder."
        : "Double-click a folder to open it, then choose Move here.";
    public FolderDestination? Destination { get; private set; }
    public DriveApiException? SessionError { get; private set; }
    public AsyncRelayCommand<FolderResponse> OpenFolderCommand { get; }
    public AsyncRelayCommand<BreadcrumbItem> NavigateBackCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event EventHandler? CloseRequested;

    public async Task InitializeAsync()
    {
        if (_disposed)
        {
            return;
        }

        CancelLoad();
        var version = _loadVersion;
        using var cancellation = new CancellationTokenSource();
        _activeLoad = cancellation;
        var parentId = Breadcrumbs.Last().Id;
        _ready = false;
        _isLoading = true;
        _errorMessage = string.Empty;
        SelectedFolder = null;
        Folders.Clear();
        NotifyState();
        try
        {
            var folders = await _apiService.GetFolderContentAsync(parentId, cancellation.Token);
            if (_disposed || version != _loadVersion || cancellation.IsCancellationRequested)
            {
                return;
            }

            // Excluding the source folder also prevents browsing into any of its descendants.
            foreach (var folder in folders.Where(folder => folder.Id != _excludedFolderId)
                         .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                Folders.Add(folder);
            }

            _ready = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!_disposed && version == _loadVersion)
            {
                HandleError(exception);
            }
        }
        finally
        {
            if (!_disposed && version == _loadVersion)
            {
                _activeLoad = null;
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private async Task OpenFolderAsync(FolderResponse? folder)
    {
        if (folder is null)
        {
            return;
        }

        Breadcrumbs.Add(new BreadcrumbItem(folder.Id, folder.Name));
        await InitializeAsync();
    }

    private async Task NavigateBackAsync(BreadcrumbItem? crumb)
    {
        if (crumb is null)
        {
            return;
        }

        while (Breadcrumbs.Last() != crumb)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        await InitializeAsync();
    }

    private void Confirm()
    {
        var crumb = Breadcrumbs.Last();
        Destination = new FolderDestination(crumb.Id, crumb.Name);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HandleError(Exception exception)
    {
        if (_disposed)
        {
            return;
        }

        _ready = false;
        if (exception is DriveApiException { StatusCode: HttpStatusCode.Unauthorized } sessionError)
        {
            SessionError = sessionError;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        _errorMessage = exception switch
        {
            DriveApiException => exception.Message,
            OperationCanceledException => "The request timed out. Please try again.",
            HttpRequestException => "Could not load folders. Check the connection and try again.",
            _ => "Could not load folders. Please try again."
        };
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(BreadcrumbTrail), nameof(CurrentFolderName), nameof(IsLoading),
                     nameof(ErrorMessage), nameof(HasError), nameof(CanBrowse), nameof(CanConfirm),
                     nameof(IsContentVisible), nameof(IsEmpty), nameof(LocationHint) })
        {
            OnPropertyChanged(property);
        }

        OpenFolderCommand.NotifyCanExecuteChanged();
        NavigateBackCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private void CancelLoad()
    {
        _loadVersion++;
        _activeLoad?.Cancel();
        _activeLoad = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelLoad();
        NotifyState();
    }
}
