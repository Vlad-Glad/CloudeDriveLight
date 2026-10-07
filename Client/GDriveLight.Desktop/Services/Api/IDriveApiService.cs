using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public interface IDriveApiService
{
    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<string?> RegisterAsync(string email, string password, string firstName, CancellationToken cancellationToken = default);
    Task<IEnumerable<FolderResponse>> GetFolderContentAsync(Guid? parentFolderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<FileResponse>> GetFilesAsync(Guid? folderId, CancellationToken cancellationToken = default);

    void SetAuthToken(string? token);
    void SetServerUrl(string url);
    string GetServerUrl();
    void Logout();
}
