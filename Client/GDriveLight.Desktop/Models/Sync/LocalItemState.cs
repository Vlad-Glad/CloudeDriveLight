namespace GDriveLight.Desktop.Models.Sync;

public class LocalItemState
{
    public long Id { get; set; }
    public Guid? ServerResourceId { get; set; }
    public bool IsDirectory { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public DateTime LocalModifiedAtUtc { get; set; }
    public long? LocalSize { get; set; }
    public string? LastSyncedHash { get; set; }
    public SyncStatus Status { get; set; }
}
