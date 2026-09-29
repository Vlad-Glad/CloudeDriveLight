public class SyncFileState
{
    private const int MaxRelativePathLength = 1024;
    private const int MaxHashLength = 128;

    public Guid Id { get; private set; }

    public Guid SyncFolderId { get; private set; }

    public Guid? DriveFileId { get; private set; }

    public string RelativePath { get; private set; }

    public string? LocalHash { get; private set; }

    public string? LastSyncedHash { get; private set; }

    public DateTime? LastSyncedAtUtc { get; private set; }

    public DateTime? LocalModifiedAtUtc { get; private set; }

    public SyncStatus Status { get; private set; }


    private SyncFileState() { }


    public SyncFileState(
        Guid syncFolderId,
        string relativePath,
        string? localHash = null,
        DateTime? localModifiedAtUtc = null,
        Guid? driveFileId = null)
    {
        ValidateGuid(syncFolderId, nameof(syncFolderId));
        ValidateRelativePath(relativePath);
        ValidateOptionalHash(localHash, nameof(localHash));
        ValidateOptionalGuid(driveFileId, nameof(driveFileId));

        Id = Guid.NewGuid();

        SyncFolderId = syncFolderId;
        RelativePath = relativePath;

        LocalHash = localHash;
        LocalModifiedAtUtc = localModifiedAtUtc;

        DriveFileId = driveFileId;

        LastSyncedHash = null;
        LastSyncedAtUtc = null;

        Status = SyncStatus.Pending;
    }


    public void UpdateLocalState(
        string localHash,
        DateTime modifiedAtUtc)
    {
        ValidateHash(localHash, nameof(localHash));

        LocalHash = localHash;
        LocalModifiedAtUtc = modifiedAtUtc;

        if (LocalHash != LastSyncedHash)
        {
            Status = SyncStatus.Pending;
        }
    }


    public void LinkDriveFile(Guid driveFileId)
    {
        ValidateGuid(driveFileId, nameof(driveFileId));

        if (DriveFileId.HasValue &&
            DriveFileId.Value != driveFileId)
        {
            throw new InvalidOperationException(
                "The synchronization state is already linked to another drive file.");
        }

        DriveFileId = driveFileId;
    }


    public void MarkPending()
    {
        Status = SyncStatus.Pending;
    }


    public void MarkSynced(
        string syncedHash,
        DateTime? syncedAtUtc = null)
    {
        ValidateHash(syncedHash, nameof(syncedHash));

        LocalHash = syncedHash;
        LastSyncedHash = syncedHash;

        LastSyncedAtUtc = syncedAtUtc ?? DateTime.UtcNow;

        Status = SyncStatus.Synced;
    }


    public void MarkConflict()
    {
        Status = SyncStatus.Conflict;
    }


    public void MarkError()
    {
        Status = SyncStatus.Error;
    }


    public bool HasLocalChanges()
    {
        return LocalHash != LastSyncedHash;
    }


    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "Relative path cannot be empty.",
                nameof(relativePath));
        }

        if (relativePath.Length > MaxRelativePathLength)
        {
            throw new ArgumentException(
                $"Relative path cannot exceed {MaxRelativePathLength} characters.",
                nameof(relativePath));
        }
    }


    private static void ValidateHash(
        string hash,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException(
                "Hash cannot be empty.",
                parameterName);
        }

        if (hash.Length > MaxHashLength)
        {
            throw new ArgumentException(
                $"Hash cannot exceed {MaxHashLength} characters.",
                parameterName);
        }
    }


    private static void ValidateOptionalHash(
        string? hash,
        string parameterName)
    {
        if (hash is null)
            return;

        ValidateHash(hash, parameterName);
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


    private static void ValidateOptionalGuid(
        Guid? value,
        string parameterName)
    {
        if (value.HasValue &&
            value.Value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{parameterName} cannot be an empty GUID.",
                parameterName);
        }
    }
}