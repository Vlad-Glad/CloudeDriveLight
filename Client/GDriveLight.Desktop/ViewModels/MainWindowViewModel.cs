using System.Collections.ObjectModel;
using System.Windows.Input;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services.Api;
using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.ViewModels;

public record BreadcrumbItem(Guid? Id, string Name);

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IDriveApiService _apiService;

    public string Title => "GDriveLight";

    // --- Sidebar Navigation ---
    private int _selectedSidebarIndex = 0;
    public int SelectedSidebarIndex
    {
        get => _selectedSidebarIndex;
        set 
        { 
            if (SetProperty(ref _selectedSidebarIndex, value))
            {
                OnPropertyChanged(nameof(IsHomeView));
                OnPropertyChanged(nameof(IsSyncedFoldersView));
                _ = LoadCurrentViewAsync();
            }
        }
    }

    public bool IsHomeView => SelectedSidebarIndex == 0;
    public bool IsSyncedFoldersView => SelectedSidebarIndex == 1;

    // --- States ---
    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set { SetProperty(ref _isLoading, value); UpdateVisibility(); } }

    private bool _isError;
    public bool IsError { get => _isError; set { SetProperty(ref _isError, value); UpdateVisibility(); } }

    private string _errorMessage = string.Empty;
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private bool _isEmpty;
    public bool IsEmpty { get => _isEmpty; set { SetProperty(ref _isEmpty, value); UpdateVisibility(); } }

    public bool IsContentVisible => !IsLoading && !IsError && !IsEmpty;

    private void UpdateVisibility() => OnPropertyChanged(nameof(IsContentVisible));

    // --- Breadcrumbs ---
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();

    // --- Files ---
    public ObservableCollection<FileItemViewModel> Files { get; } = new();
    private FileItemViewModel? _selectedFile;
    public FileItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    // --- Commands ---
    public ICommand RetryCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand NavigateBackCommand { get; }
    public ICommand LogoutCommand { get; }

    public MainWindowViewModel(IDriveApiService apiService)
    {
        _apiService = apiService;
        
        RetryCommand = new RelayCommand(async () => await LoadCurrentViewAsync());
        OpenFolderCommand = new RelayCommand<FileItemViewModel>(OpenFile);
        NavigateBackCommand = new RelayCommand<BreadcrumbItem>(NavigateBack);
        LogoutCommand = new RelayCommand(Logout);

        _ = InitializeAndLoadAsync();
    }

    private void Logout()
    {
        _apiService.Logout();
        
        if (System.Windows.Application.Current is App app)
        {
            var oldWindow = app.MainWindow;
            app.ShowLoginFlow();
            oldWindow?.Close();
        }
    }

    private async Task InitializeAndLoadAsync()
    {
        await LoadCurrentViewAsync();
    }

    private async Task LoadCurrentViewAsync()
    {
        if (IsSyncedFoldersView)
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem(null, "Synced folders"));
            Files.Clear();
            IsLoading = false;
            IsError = false;
            IsEmpty = true;
            return;
        }

        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BreadcrumbItem(null, "Home"));
        await LoadFolderDataAsync();
    }

    private async Task LoadFolderDataAsync()
    {
        IsLoading = true;
        IsError = false;
        IsEmpty = false;
        Files.Clear();

        try
        {
            var currentFolderId = Breadcrumbs.Last().Id;
            
            var folders = await _apiService.GetFolderContentAsync(currentFolderId);
            var files = await _apiService.GetFilesAsync(currentFolderId);

            foreach (var f in folders.OrderBy(x => x.Name))
            {
                Files.Add(new FileItemViewModel 
                { 
                    Id = f.Id, 
                    Name = f.Name, 
                    DateModified = "—", 
                    Size = "—", 
                    Icon = SymbolRegular.Folder24, 
                    IconColorBrushKey = "#5F6368", 
                    IsFolder = true 
                });
            }

            foreach (var f in files.OrderBy(x => x.Name))
            {
                Files.Add(new FileItemViewModel 
                { 
                    Id = f.Id, 
                    Name = f.Name, 
                    DateModified = f.UploadedAtUtc.ToLocalTime().ToString("d"), 
                    Size = "—", // Size needs to be returned by API or handled otherwise
                    Icon = GetIconForFileType(f.FileTypeId), 
                    IconColorBrushKey = GetColorForFileType(f.FileTypeId), 
                    IsFolder = false 
                });
            }

            IsEmpty = Files.Count == 0;
        }
        catch (Exception ex)
        {
            IsError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private SymbolRegular GetIconForFileType(int fileTypeId)
    {
        // 1=Document, 2=Image, 3=Video, etc. Map accordingly.
        return fileTypeId switch
        {
            1 => SymbolRegular.Document24,
            2 => SymbolRegular.Image24,
            3 => SymbolRegular.Video24,
            4 => SymbolRegular.Table24,
            _ => SymbolRegular.Document24
        };
    }

    private string GetColorForFileType(int fileTypeId)
    {
        return fileTypeId switch
        {
            1 => "#185ABD", // Blue for docs
            2 => "#A142F4", // Purple for images
            3 => "#EA4335", // Red for video
            4 => "#107C41", // Green for sheets
            _ => "#5F6368"
        };
    }

    private async void OpenFile(FileItemViewModel? file)
    {
        if (file == null || !file.IsFolder) return;

        Breadcrumbs.Add(new BreadcrumbItem(file.Id, file.Name));
        await LoadFolderDataAsync();
    }

    private async void NavigateBack(BreadcrumbItem? targetBreadcrumb)
    {
        if (targetBreadcrumb == null) return;

        var index = Breadcrumbs.IndexOf(targetBreadcrumb);
        if (index == -1 || index == Breadcrumbs.Count - 1) return;

        while (Breadcrumbs.Count > index + 1)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        await LoadFolderDataAsync();
    }
}
