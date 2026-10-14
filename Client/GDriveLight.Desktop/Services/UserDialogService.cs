using System.Windows;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using GDriveLight.Desktop.Views;

namespace GDriveLight.Desktop.Services;

public sealed class UserDialogService(IDriveApiService apiService) : IUserDialogService
{
    public FolderDestination? SelectDestination(Guid? currentFolderId, Guid? excludedFolderId)
    {
        using var model = new FolderPickerViewModel(apiService, currentFolderId, excludedFolderId);
        var window = new FolderPickerWindow(model) { Owner = Application.Current?.MainWindow };
        var confirmed = window.ShowDialog() == true;
        if (model.SessionError is not null)
        {
            throw model.SessionError;
        }

        return confirmed ? model.Destination : null;
    }

    public string? RequestName(string title, string prompt, string initialName = "")
    {
        var window = new NameDialogWindow(title, prompt, initialName)
        {
            Owner = Application.Current?.MainWindow
        };
        return window.ShowDialog() == true ? window.EnteredName : null;
    }

    public bool ConfirmDisconnectDevice()
    {
        const string message = "Stop synchronizing this device?\n\nFiles on this computer will stay in place. " +
            "The cloud sync folder will become a separate folder in Home. Changes that have not synchronized will remain only on this computer. " +
            "Your account will stay signed in, and another account can connect this device.";
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, "Disconnect device", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, "Disconnect device", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
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
