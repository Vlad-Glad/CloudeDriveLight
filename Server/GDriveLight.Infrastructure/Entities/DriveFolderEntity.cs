namespace GDriveLight.Infrastructure.Entities;

public class DriveFolderEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public DriveFolderEntity? ParentFolder { get; set; }
    public Guid OwnerId { get; set; }
    public ApplicationUserEntity Owner { get; set; } = null!;
    
    public ICollection<DriveFolderEntity> SubFolders { get; set; } = new List<DriveFolderEntity>();
    public ICollection<DriveFileEntity> Files { get; set; } = new List<DriveFileEntity>();
}
