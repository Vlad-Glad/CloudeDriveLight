namespace GDriveLight.Domain.Models;

public class DriveFolder
{
    private const int MaxNameLength = 255;

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Guid? ParentFolderId { get; private set; }

    public Guid OwnerId { get; private set; }


    public DriveFolder(
        string name,
        Guid ownerId,
        Guid? parentFolderId = null)
    {
        ValidateName(name);
        ValidateGuid(ownerId, nameof(ownerId));
        ValidateParentFolderId(parentFolderId);

        Id = Guid.NewGuid();

        Name = name;
        OwnerId = ownerId;
        ParentFolderId = parentFolderId;
    }


    public void Rename(string newName)
    {
        ValidateName(newName);

        Name = newName;
    }


    public void MoveTo(Guid? parentFolderId)
    {
        ValidateParentFolderId(parentFolderId);

        if (parentFolderId == Id)
        {
            throw new ArgumentException(
                "A folder cannot be its own parent.",
                nameof(parentFolderId));
        }

        ParentFolderId = parentFolderId;
    }


    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Folder name cannot be empty.",
                nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Folder name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }
    }


    private static void ValidateParentFolderId(Guid? parentFolderId)
    {
        if (parentFolderId.HasValue &&
            parentFolderId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Parent folder ID cannot be an empty GUID.",
                nameof(parentFolderId));
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