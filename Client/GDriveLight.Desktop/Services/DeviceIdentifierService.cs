using System.IO;

namespace GDriveLight.Desktop.Services;

public class DeviceIdentifierService : IDeviceIdentifierService
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private Guid? _cachedDeviceId;

    public DeviceIdentifierService(string? customFolderOrFilePath = null)
    {
        if (customFolderOrFilePath == null)
        {
            _filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GDriveLight",
                "device.id");
        }
        else if (Directory.Exists(customFolderOrFilePath) || !Path.HasExtension(customFolderOrFilePath))
        {
            _filePath = Path.Combine(customFolderOrFilePath, "device.id");
        }
        else
        {
            _filePath = customFolderOrFilePath;
        }
    }

    public Guid GetDeviceId()
    {
        lock (_lock)
        {
            if (_cachedDeviceId.HasValue)
            {
                return _cachedDeviceId.Value;
            }

            try
            {
                if (File.Exists(_filePath))
                {
                    var text = File.ReadAllText(_filePath).Trim();
                    if (Guid.TryParse(text, out var parsed))
                    {
                        _cachedDeviceId = parsed;
                        return parsed;
                    }
                }

                var newId = Guid.NewGuid();
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(_filePath, newId.ToString());
                _cachedDeviceId = newId;
                return newId;
            }
            catch
            {
                var fallback = Guid.NewGuid();
                _cachedDeviceId = fallback;
                return fallback;
            }
        }
    }
}
