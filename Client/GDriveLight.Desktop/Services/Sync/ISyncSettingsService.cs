namespace GDriveLight.Desktop.Services.Sync;

public interface ISyncSettingsService
{
    string GetSyncFolderPath(Guid userId);
    void SetSyncFolderPath(Guid userId, string folderPath);
    void ClearSyncFolderPath(Guid userId);
}
