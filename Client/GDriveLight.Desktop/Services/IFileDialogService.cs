namespace GDriveLight.Desktop.Services;

public interface IFileDialogService
{
    IReadOnlyList<string> SelectFilesToUpload();

    string? SelectDownloadDestination(string suggestedFileName);

    string? SelectFolder(string? initialDirectory = null);
}
