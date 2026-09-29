namespace GDriveLight.Infrastructure.Entities;

public class SyncFolderEntity
{
    public Guid Id { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    
    public Guid DriveFolderId { get; set; }
    public DriveFolderEntity DriveFolder { get; set; } = null!;
    
    public Guid DeviceId { get; set; }
    public DeviceEntity Device { get; set; } = null!;
    
    public DateTime? LastSyncedAtUtc { get; set; }
    
    public int SyncMode { get; set; } // Mapping domain enum SyncMode to int
}
