public class DriveFile
{
    private const int MaxNameLength = 255;
    private const int MaxFileUrlLength = 2048;
    private const int MaxContentHashLength = 128;

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string FileUrl { get; private set; }

    public int FileTypeId { get; private set; }

    public string ContentHash { get; private set; }

    public Guid? FolderId { get; private set; }

    public Guid OwnerId { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    public Guid? EditedByUserId { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }


    private DriveFile() { }


    public DriveFile(
        string name,
        string fileUrl,
        int fileTypeId,
        string contentHash,
        Guid ownerId,
        Guid? folderId = null)
    {
        ValidateName(name);
        ValidateFileUrl(fileUrl);
        ValidateFileTypeId(fileTypeId);
        ValidateContentHash(contentHash);
        ValidateGuid(ownerId, nameof(ownerId));

        if (folderId.HasValue && folderId.Value == Guid.Empty)
            throw new ArgumentException(
                "Folder ID cannot be an empty GUID.",
                nameof(folderId));

        Id = Guid.NewGuid();

        Name = name;
        FileUrl = fileUrl;
        FileTypeId = fileTypeId;
        ContentHash = contentHash;

        OwnerId = ownerId;
        FolderId = folderId;

        UploadedAtUtc = DateTime.UtcNow;
    }


    public void Rename(string newName)
    {
        ValidateName(newName);

        Name = newName;
    }


    public void MoveTo(Guid? folderId)
    {
        if (folderId.HasValue && folderId.Value == Guid.Empty)
            throw new ArgumentException(
                "Folder ID cannot be an empty GUID.",
                nameof(folderId));

        FolderId = folderId;
    }


    public void ChangeFileType(int fileTypeId)
    {
        ValidateFileTypeId(fileTypeId);

        FileTypeId = fileTypeId;
    }


    public void UpdateContent(
        string fileUrl,
        string contentHash,
        Guid editedByUserId)
    {
        ValidateFileUrl(fileUrl);
        ValidateContentHash(contentHash);
        ValidateGuid(editedByUserId, nameof(editedByUserId));

        FileUrl = fileUrl;
        ContentHash = contentHash;

        EditedByUserId = editedByUserId;
        EditedAtUtc = DateTime.UtcNow;
    }


    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "File name cannot be empty.",
                nameof(name));

        if (name.Length > MaxNameLength)
            throw new ArgumentException(
                $"File name cannot exceed {MaxNameLength} characters.",
                nameof(name));
    }


    private static void ValidateFileUrl(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException(
                "File URL cannot be empty.",
                nameof(fileUrl));

        if (fileUrl.Length > MaxFileUrlLength)
            throw new ArgumentException(
                $"File URL cannot exceed {MaxFileUrlLength} characters.",
                nameof(fileUrl));
    }


    private static void ValidateContentHash(string contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException(
                "Content hash cannot be empty.",
                nameof(contentHash));

        if (contentHash.Length > MaxContentHashLength)
            throw new ArgumentException(
                $"Content hash cannot exceed {MaxContentHashLength} characters.",
                nameof(contentHash));
    }


    private static void ValidateFileTypeId(int fileTypeId)
    {
        if (fileTypeId <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(fileTypeId),
                "File type ID must be greater than zero.");
    }


    private static void ValidateGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                $"{parameterName} cannot be an empty GUID.",
                parameterName);
    }
}