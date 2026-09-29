using GDriveLight.Domain.Enums;

namespace GDriveLight.Infrastructure.Entities;

public class SyncFileStateEntity
{
    public Guid Id { get; set; }
    
    public Guid SyncFolderId { get; set; }
    public SyncFolderEntity SyncFolder { get; set; } = null!;
    
    public Guid? DriveFileId { get; set; }
    public DriveFileEntity? DriveFile { get; set; }
    
    public string RelativePath { get; set; } = string.Empty;
    public string? LocalHash { get; set; }
    public string? LastSyncedHash { get; set; }
    
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? LocalModifiedAtUtc { get; set; }
    
    public SyncStatus Status { get; set; }
}
