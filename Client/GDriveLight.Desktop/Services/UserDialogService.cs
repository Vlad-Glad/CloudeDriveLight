using System.Windows;
using GDriveLight.Desktop.Views;

namespace GDriveLight.Desktop.Services;

public sealed class UserDialogService : IUserDialogService
{
    public string? RequestName(string title, string prompt, string initialName = "")
    {
        var window = new NameDialogWindow(title, prompt, initialName)
        {
            Owner = Application.Current?.MainWindow
        };
        return window.ShowDialog() == true ? window.EnteredName : null;
    }

    public bool ConfirmDelete(string name, bool isFolder)
    {
        var message = isFolder
            ? $"Permanently delete '{name}' and all files and folders inside it? This cannot be undone."
            : $"Permanently delete '{name}'? This cannot be undone.";
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
