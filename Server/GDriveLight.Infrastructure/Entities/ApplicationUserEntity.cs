using Microsoft.AspNetCore.Identity;

namespace GDriveLight.Infrastructure.Entities;

public class ApplicationUserEntity : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public ICollection<DeviceEntity> Devices { get; set; } = new List<DeviceEntity>();
    public ICollection<DriveFolderEntity> OwnedFolders { get; set; } = new List<DriveFolderEntity>();
    public ICollection<DriveFileEntity> OwnedFiles { get; set; } = new List<DriveFileEntity>();
}
