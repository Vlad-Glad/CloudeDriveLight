using System.Text.Json.Serialization;

namespace GDriveLight.Desktop.Models.Sync;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeType
{
    Created = 0,
    Modified = 1,
    Renamed = 2,
    Moved = 3,
    Deleted = 4
}
