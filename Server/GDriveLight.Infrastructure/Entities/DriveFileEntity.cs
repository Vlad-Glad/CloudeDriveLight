namespace GDriveLight.Infrastructure.Entities;

public class DriveFileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    
    public int FileTypeId { get; set; }
    public FileTypeEntity FileType { get; set; } = null!;
    
    public string ContentHash { get; set; } = string.Empty;
    
    public Guid? FolderId { get; set; }
    public DriveFolderEntity? Folder { get; set; }
    
    public Guid OwnerId { get; set; }
    public ApplicationUserEntity Owner { get; set; } = null!;
    
    public DateTime UploadedAtUtc { get; set; }
    
    public Guid? EditedByUserId { get; set; }
    public ApplicationUserEntity? EditedByUser { get; set; }
    
    public DateTime? EditedAtUtc { get; set; }
}
