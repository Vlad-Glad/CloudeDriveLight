using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.ViewModels;

public record BreadcrumbItem(Guid? Id, string Name);

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IDriveApiService _apiService;
    private CancellationTokenSource? _activeLoad;
    private long _loadVersion;
    private bool _disposed;
    private int _selectedSidebarIndex;
    private bool _isLoading;
    private bool _isError;
    private string _errorMessage = string.Empty;
    private FileItemViewModel? _selectedFile;
    private string _searchText = string.Empty;

    public MainWindowViewModel(IDriveApiService apiService, IFileDialogService fileDialogs, IUserDialogService dialogs)
    {
        _apiService = apiService;
        _fileDialogs = fileDialogs;
        _dialogs = dialogs;
        Breadcrumbs.Add(new BreadcrumbItem(null, "Home"));
        RetryCommand = new AsyncRelayCommand(RefreshAsync, HandleError, () => !_disposed && IsHomeView && !IsBusy);
        OpenFolderCommand = new AsyncRelayCommand<FileItemViewModel>(OpenFolderAsync, HandleError,
            file => CanManageFiles && file?.IsFolder == true);
        NavigateBackCommand = new AsyncRelayCommand<BreadcrumbItem>(NavigateBackAsync, HandleError,
            crumb => !_disposed && IsHomeView && !IsBusy && crumb is not null &&
                Breadcrumbs.Contains(crumb) && !ReferenceEquals(crumb, Breadcrumbs.LastOrDefault()));
        LogoutCommand = new RelayCommand(Logout, () => !_disposed);
        InitializeOperationCommands();
    }

    public string Title => "GDriveLight";
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
    public IEnumerable<BreadcrumbItem> BreadcrumbTrail => Breadcrumbs.Take(Math.Max(0, Breadcrumbs.Count - 1));
    public string CurrentFolderName => Breadcrumbs.LastOrDefault()?.Name ?? "Home";
    public ObservableCollection<FileItemViewModel> Files { get; } = new();
    public ObservableCollection<FileItemViewModel> VisibleFiles { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplySearch();
            }
        }
    }

    public int SelectedSidebarIndex
    {
        get => _selectedSidebarIndex;
        set
        {
            if (!CanNavigate || value is < 0 or > 1 || !SetProperty(ref _selectedSidebarIndex, value))
            {
                return;
            }

            CancelLoad();
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem(null, value == 0 ? "Home" : "Synced folders"));
            Files.Clear();
            SearchText = string.Empty;
            ApplySearch();
            SelectedFile = null;
            IsError = false;
            IsLoading = false;
            NotifyState();
            if (IsHomeView)
            {
                // RefreshAsync handles errors and discards cancelled or superseded results.
                _ = RefreshAsync();
            }
        }
    }

    public bool IsHomeView => SelectedSidebarIndex == 0;
    public bool IsSyncedFoldersView => SelectedSidebarIndex == 1;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyState();
            }
        }
    }

    public bool IsError
    {
        get => _isError;
        private set
        {
            if (SetProperty(ref _isError, value))
            {
                NotifyState();
            }
        }
    }

    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsEmpty => !IsLoading && !IsError && VisibleFiles.Count == 0;
    public bool IsContentVisible => !IsLoading && !IsError && VisibleFiles.Count > 0;
    public string EmptyTitle => IsSyncedFoldersView ? "Synced folders" : string.IsNullOrWhiteSpace(SearchText) ? "This folder is empty" : "No matching files";
    public string EmptyDescription => IsSyncedFoldersView
        ? "Devices and synced folders will appear here in a later version."
        : string.IsNullOrWhiteSpace(SearchText) ? "Use New to add folders and files." : "Try another name or clear the search.";

    public FileItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand<FileItemViewModel> OpenFolderCommand { get; }
    public AsyncRelayCommand<BreadcrumbItem> NavigateBackCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public event EventHandler? SessionEnded;

    public Task InitializeAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        if (_disposed || !IsHomeView)
        {
            return;
        }

        CancelLoad();
        var version = _loadVersion;
        using var cancellation = new CancellationTokenSource();
        _activeLoad = cancellation;
        var folderId = Breadcrumbs.Last().Id;
        IsLoading = true;
        IsError = false;
        ErrorMessage = string.Empty;
        SelectedFile = null;

        try
        {
            var foldersTask = _apiService.GetFolderContentAsync(folderId, cancellation.Token);
            var filesTask = _apiService.GetFilesAsync(folderId, cancellation.Token);
            await Task.WhenAll(foldersTask, filesTask);
            if (_disposed || version != _loadVersion || !IsHomeView)
            {
                return;
            }

            Files.Clear();
            foreach (var folder in (await foldersTask).OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Files.Add(new FileItemViewModel
                {
                    Id = folder.Id, Name = folder.Name, IsFolder = true, DateModified = "—", Size = "—",
                    Icon = SymbolRegular.Folder24, IconColorBrushKey = "#5F6368"
                });
            }

            foreach (var file in (await filesTask).OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var (icon, color) = GetFileAppearance(file.Name);
                Files.Add(new FileItemViewModel
                {
                    Id = file.Id, Name = file.Name, DateModified = (file.EditedAtUtc ?? file.UploadedAtUtc).ToLocalTime().ToString("d"),
                    Size = "—", Icon = icon, IconColorBrushKey = color
                });
            }

            ApplySearch();
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
            if (ReferenceEquals(_activeLoad, cancellation))
            {
                _activeLoad = null;
                IsLoading = false;
                NotifyState();
            }
        }
    }

    private async Task OpenFolderAsync(FileItemViewModel? file)
    {
        if (file is null || !file.IsFolder || !Files.Contains(file))
        {
            return;
        }

        Breadcrumbs.Add(new BreadcrumbItem(file.Id, file.Name));
        SearchText = string.Empty;
        await RefreshAsync();
    }

    private async Task NavigateBackAsync(BreadcrumbItem? target)
    {
        if (target is null)
        {
            return;
        }

        var index = Breadcrumbs.IndexOf(target);
        if (index < 0)
        {
            return;
        }

        while (Breadcrumbs.Count > index + 1)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        SearchText = string.Empty;

        await RefreshAsync();
    }

    private void Logout()
    {
        CancelLoad();
        _activeOperation?.Cancel();
        _apiService.Logout();
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    private void HandleError(Exception exception)
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

        IsError = true;
        ErrorMessage = exception switch
        {
            DriveApiException => exception.Message,
            OperationCanceledException => "The request timed out. Please retry.",
            HttpRequestException => "Could not connect to the server. Check the connection and retry.",
            JsonException => "The server returned an invalid response.",
            _ => "The folder could not be loaded. Please retry."
        };
    }

    private void CancelLoad()
    {
        _loadVersion++;
        _activeLoad?.Cancel();
        _activeLoad = null;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(BreadcrumbTrail));
        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(IsHomeView));
        OnPropertyChanged(nameof(IsSyncedFoldersView));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsContentVisible));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
        OpenFolderCommand?.NotifyCanExecuteChanged();
        NavigateBackCommand?.NotifyCanExecuteChanged();
        RetryCommand?.NotifyCanExecuteChanged();
        CreateFolderCommand?.NotifyCanExecuteChanged();
        UploadCommand?.NotifyCanExecuteChanged();
        DownloadCommand?.NotifyCanExecuteChanged();
        RenameCommand?.NotifyCanExecuteChanged();
        DeleteCommand?.NotifyCanExecuteChanged();
        MoveCommand?.NotifyCanExecuteChanged();
        CancelOperationCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(CanManageFiles));
    }

    private void ApplySearch()
    {
        VisibleFiles.Clear();
        var query = SearchText.Trim();
        foreach (var file in Files.Where(file => file.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleFiles.Add(file);
        }

        if (SelectedFile is not null && !VisibleFiles.Contains(SelectedFile))
        {
            SelectedFile = null;
        }

        NotifyState();
    }

    private static (SymbolRegular Icon, string Color) GetFileAppearance(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => (SymbolRegular.Image24, "#A142F4"),
            ".mp4" or ".mov" or ".avi" => (SymbolRegular.Video24, "#EA4335"),
            ".xlsx" or ".xls" or ".csv" => (SymbolRegular.Table24, "#107C41"),
            ".pdf" => (SymbolRegular.Document24, "#EA4335"),
            ".doc" or ".docx" => (SymbolRegular.Document24, "#185ABD"),
            _ => (SymbolRegular.Document24, "#5F6368")
        };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelLoad();
        _activeOperation?.Cancel();
    }
}
