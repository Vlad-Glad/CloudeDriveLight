using System.IO;
using GDriveLight.Desktop.Services;

namespace GDriveLight.Desktop.Tests.Services;

public class DeviceIdentifierServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public DeviceIdentifierServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GDriveLightTests_DeviceId_" + Guid.NewGuid());
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
    public void GetDeviceId_GeneratesAndPersistsSameIdAcrossInstances()
    {
        var service1 = new DeviceIdentifierService(_testDirectory);
        var id1 = service1.GetDeviceId();

        Assert.NotEqual(Guid.Empty, id1);

        var service2 = new DeviceIdentifierService(_testDirectory);
        var id2 = service2.GetDeviceId();

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GetDeviceId_ReadsExistingValidFile()
    {
        var expectedGuid = Guid.NewGuid();
        var filePath = Path.Combine(_testDirectory, "device.id");
        File.WriteAllText(filePath, expectedGuid.ToString());

        var service = new DeviceIdentifierService(_testDirectory);
        var actual = service.GetDeviceId();

        Assert.Equal(expectedGuid, actual);
    }
}
