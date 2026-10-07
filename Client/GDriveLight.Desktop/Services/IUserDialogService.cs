namespace GDriveLight.Desktop.Services;

public interface IUserDialogService
{
    string? RequestName(string title, string prompt, string initialName = "");
    bool ConfirmDelete(string name, bool isFolder);
}
