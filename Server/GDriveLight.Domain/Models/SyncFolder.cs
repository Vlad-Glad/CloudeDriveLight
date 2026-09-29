public class SyncFolder
{
    private const int MaxLocalPathLength = 1024;

    public Guid Id { get; private set; }

    public string LocalPath { get; private set; }

    public Guid DriveFolderId { get; private set; }
    public DriveFolder DriveFolder { get; private set; } = null!;

    public Guid DeviceId { get; private set; }
    public Device Device { get; private set; } = null!;

    public DateTime? LastSyncedAtUtc { get; private set; }

    public SyncMode SyncMode { get; private set; }


    private SyncFolder() { }


    public SyncFolder(
        string localPath,
        Guid driveFolderId,
        Guid deviceId,
        SyncMode syncMode)
    {
        ValidateLocalPath(localPath);
        ValidateGuid(driveFolderId, nameof(driveFolderId));
        ValidateGuid(deviceId, nameof(deviceId));
        ValidateSyncMode(syncMode);

        Id = Guid.NewGuid();

        LocalPath = localPath;
        DriveFolderId = driveFolderId;
        DeviceId = deviceId;
        SyncMode = syncMode;

        LastSyncedAtUtc = null;
    }


    public void ChangeLocalPath(string newLocalPath)
    {
        ValidateLocalPath(newLocalPath);

        LocalPath = newLocalPath;
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


    private static void ValidateLocalPath(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException(
                "Local path cannot be empty.",
                nameof(localPath));
        }

        if (localPath.Length > MaxLocalPathLength)
        {
            throw new ArgumentException(
                $"Local path cannot exceed {MaxLocalPathLength} characters.",
                nameof(localPath));
        }
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