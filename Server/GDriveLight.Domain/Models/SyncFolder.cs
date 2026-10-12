using GDriveLight.Domain.Enums;

namespace GDriveLight.Domain.Models;

public class SyncFolder
{
    public Guid Id { get; private set; }

    public Guid DriveFolderId { get; private set; }

    public Guid DeviceId { get; private set; }

    public DateTime? LastSyncedAtUtc { get; private set; }

    public SyncMode SyncMode { get; private set; }


    public SyncFolder(
        Guid driveFolderId,
        Guid deviceId,
        SyncMode syncMode)
    {
        ValidateGuid(driveFolderId, nameof(driveFolderId));
        ValidateGuid(deviceId, nameof(deviceId));
        ValidateSyncMode(syncMode);

        Id = Guid.NewGuid();

        DriveFolderId = driveFolderId;
        DeviceId = deviceId;
        SyncMode = syncMode;

        LastSyncedAtUtc = null;
    }


    public void ChangeSyncMode(SyncMode newSyncMode)
    {
        ValidateSyncMode(newSyncMode);

        SyncMode = newSyncMode;
    }


    public void MarkSynced()
    {
        LastSyncedAtUtc = DateTime.UtcNow;
    }


    private static void ValidateSyncMode(SyncMode syncMode)
    {
        if (!Enum.IsDefined(syncMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(syncMode),
                "Invalid synchronization mode.");
        }
    }


    private static void ValidateGuid(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{parameterName} cannot be an empty GUID.",
                parameterName);
        }
    }
}