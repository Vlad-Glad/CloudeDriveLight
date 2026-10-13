using System.IO;
using GDriveLight.Desktop.Services.Sync;

namespace GDriveLight.Desktop.Tests.Services;

public class SyncSettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public SyncSettingsServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GDriveLightTests_SyncSettings_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetSyncFolderPath_ReturnsDefaultFolder_WhenNotSet()
    {
        var service = new SyncSettingsService(_testDirectory);
        var userId = Guid.NewGuid();

        var path = service.GetSyncFolderPath(userId);

        Assert.NotNull(path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SetAndGetSyncFolderPath_PersistsCustomFolder()
    {
        var service = new SyncSettingsService(_testDirectory);
        var userId = Guid.NewGuid();
        var customFolder = Path.Combine(_testDirectory, "CustomSyncFolder");
        Directory.CreateDirectory(customFolder);

        service.SetSyncFolderPath(userId, customFolder);

        var retrieved = service.GetSyncFolderPath(userId);
        Assert.Equal(customFolder, retrieved);
    }
}
