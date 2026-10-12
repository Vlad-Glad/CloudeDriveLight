namespace GDriveLight.Domain.Enums;

public enum SyncStatus
{
    Synced,
    PendingUpload,
    PendingDownload,
    Pending,
    Conflict,
    Error
}
