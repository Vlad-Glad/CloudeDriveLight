using System.Text.Json.Serialization;

namespace GDriveLight.Desktop.Models.Sync;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourceType
{
    File = 0,
    Folder = 1
}
