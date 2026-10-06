using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public interface IDriveApiService
{
    Task<string?> LoginAsync(string email, string password);
    Task<string?> RegisterAsync(string email, string password, string firstName);
    Task<IEnumerable<FolderResponse>> GetFolderContentAsync(Guid? parentFolderId);
    Task<IEnumerable<FileResponse>> GetFilesAsync(Guid? folderId);
    
    void SetAuthToken(string? token);
    void SetServerUrl(string url);
    string GetServerUrl();
    void Logout();
}
