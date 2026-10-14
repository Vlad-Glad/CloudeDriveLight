namespace GDriveLight.Desktop.Models.Sync;

public class SyncScope
{
    public int Id { get; set; } = 1;
    public Guid ServerRootFolderId { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public bool InitialSyncCompleted { get; set; }
}
