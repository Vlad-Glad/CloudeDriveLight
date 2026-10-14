using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private FileTypeResponse? _selectedFileType;

    public FileTypeResponse? SelectedFileType
    {
        get => _selectedFileType;
        set
        {
            if (!SetProperty(ref _selectedFileType, value)) return;
            OnPropertyChanged(nameof(FileTypeFilterLabel));
            ApplyFileTypeFilter();
        }
    }

    public string FileTypeFilterLabel => $"Type: {SelectedFileType?.Extension ?? "All types"} ▾";

    public async Task<IReadOnlyList<FileTypeResponse>?> LoadFileTypesAsync()
    {
        if (_disposed) return null;
        try
        {
            var types = await _apiService.GetFileTypesAsync(_connectionLifetime.Token);
            return _disposed ? null : types.OrderBy(type => type.Extension, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) { HandleOperationError(exception); }
        return null;
    }

    private void ApplyFileTypeFilter()
    {
        VisibleFiles.Clear();
        foreach (var file in Files.Where(file => file.IsFolder || SelectedFileType is null || file.FileTypeId == SelectedFileType.Id))
        {
            VisibleFiles.Add(file);
        }
        if (SelectedFile is not null && !VisibleFiles.Contains(SelectedFile)) SelectedFile = null;
        NotifyState();
    }
}
