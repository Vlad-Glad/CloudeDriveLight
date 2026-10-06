namespace GDriveLight.Desktop.Services;

public interface IFileDialogService
{
    IReadOnlyList<string> SelectFilesToUpload();

    string? SelectDownloadDestination(string suggestedFileName);
}
