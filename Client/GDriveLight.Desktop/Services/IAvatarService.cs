using System.Windows.Media;

namespace GDriveLight.Desktop.Services;

public interface IAvatarService
{
    Task<ImageSource?> GetAvatarAsync(string email, CancellationToken cancellationToken = default);
}
