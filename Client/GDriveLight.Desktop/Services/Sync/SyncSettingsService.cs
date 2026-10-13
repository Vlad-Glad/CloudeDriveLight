using System.IO;

namespace GDriveLight.Desktop.Services.Sync;

public class SyncSettingsService : ISyncSettingsService
{
    private readonly string _settingsFolder;

    public SyncSettingsService(string? customSettingsFolder = null)
    {
        _settingsFolder = customSettingsFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GDriveLight");
    }

    public string GetSyncFolderPath(Guid userId)
    {
        var settingsFile = Path.Combine(_settingsFolder, $"sync_folder_{userId}.txt");
        if (File.Exists(settingsFile))
        {
            var path = File.ReadAllText(settingsFile).Trim();
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                return path;
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "GDriveLight");

        if (!Directory.Exists(defaultPath))
        {
            Directory.CreateDirectory(defaultPath);
        }

        return defaultPath;
    }

    public void SetSyncFolderPath(Guid userId, string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(_settingsFolder))
        {
            Directory.CreateDirectory(_settingsFolder);
        }

        var settingsFile = Path.Combine(_settingsFolder, $"sync_folder_{userId}.txt");
        File.WriteAllText(settingsFile, folderPath.Trim());
    }
}
