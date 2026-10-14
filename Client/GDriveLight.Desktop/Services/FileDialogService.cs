using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace GDriveLight.Desktop.Services;

public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> SelectFilesToUpload()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select files",
            Multiselect = true,
            CheckFileExists = true
        };

        return ShowDialog(dialog) ? dialog.FileNames : Array.Empty<string>();
    }

    public string? SelectDownloadDestination(string suggestedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        var dialog = new SaveFileDialog
        {
            Title = "Save file",
            FileName = Path.GetFileName(suggestedFileName),
            OverwritePrompt = true
        };

        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    public string? SelectFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder for Synchronization",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty
        };

        return ShowDialog(dialog) ? dialog.FolderName : null;
    }

    private static bool ShowDialog(CommonDialog dialog)
    {
        var owner = Application.Current?.MainWindow;
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
    }
}
