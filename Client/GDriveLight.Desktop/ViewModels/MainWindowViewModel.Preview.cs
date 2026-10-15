using GDriveLight.Desktop.Commands;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public RelayCommand<FileItemViewModel> OpenItemCommand { get; private set; } = null!;
    public RelayCommand<FileItemViewModel> PreviewCommand { get; private set; } = null!;
    public event EventHandler<FilePreviewViewModel>? PreviewRequested;

    private void InitializePreviewCommands()
    {
        PreviewCommand = new RelayCommand<FileItemViewModel>(file =>
        {
            if (file is null || PreviewRequested is null) return;
            var preview = new FilePreviewViewModel(_apiService, _fileDialogs, VisibleFiles, file.Id);
            preview.SessionExpired += (_, _) => Logout();
            PreviewRequested.Invoke(this, preview);
        }, file => CanManageFiles && file is { IsFolder: false } && VisibleFiles.Contains(file));
        OpenItemCommand = new RelayCommand<FileItemViewModel>(file =>
        {
            if (file?.IsFolder == true) OpenFolderCommand.Execute(file);
            else PreviewCommand.Execute(file);
        }, file => CanManageFiles && file is not null && VisibleFiles.Contains(file));
    }
}
