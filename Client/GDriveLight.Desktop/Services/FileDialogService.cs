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
            Title = "Виберіть файли",
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
            Title = "Зберегти файл",
            FileName = Path.GetFileName(suggestedFileName),
            OverwritePrompt = true
        };

        return ShowDialog(dialog) ? dialog.FileName : null;
    }

    private static bool ShowDialog(CommonDialog dialog)
    {
        var owner = Application.Current?.MainWindow;
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
    }
}
