using System.IO;
using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public interface IDriveApiService
{
    Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<string?> RegisterAsync(string email, string password, string firstName, CancellationToken cancellationToken = default);
    Task<IEnumerable<FolderResponse>> GetFolderContentAsync(Guid? parentFolderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<FileResponse>> GetFilesAsync(Guid? folderId, CancellationToken cancellationToken = default);
    Task<Guid> CreateFolderAsync(string name, Guid? parentFolderId, CancellationToken cancellationToken = default);
    Task<Guid> CreateFolderAsync(string name, Guid? parentFolderId, bool deviceOrigin, CancellationToken cancellationToken = default);
    Task<Guid> UploadFileAsync(string filePath, Guid? folderId, CancellationToken cancellationToken = default);
    Task<Guid> UploadFileAsync(string filePath, Guid? folderId, bool deviceOrigin, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(Guid fileId, string destination, CancellationToken cancellationToken = default);
    Task<IEnumerable<FolderResponse>> GetFolderPathAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<FolderResponse>> SearchFoldersAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<FileResponse>> SearchFilesAsync(string name, CancellationToken cancellationToken = default);

    Task RenameAsync(Guid id, bool isFolder, string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, bool isFolder, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, bool isFolder, bool deviceOrigin, CancellationToken cancellationToken = default);
    Task MoveAsync(Guid id, bool isFolder, Guid? destinationFolderId, CancellationToken cancellationToken = default);

    void SetAuthToken(string? token);
    void Logout();

    Task<IEnumerable<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<DeviceResponse> ConfigureSyncFolderAsync(string localPath, CancellationToken cancellationToken = default);
    Guid DeviceId { get; }
    Guid? CurrentUserId { get; }
    UserProfile? CurrentUser { get; }
    Task<SyncChangesResponse> GetChangesAsync(long cursor, int limit = 100, CancellationToken cancellationToken = default);
    Task UpdateFileContentAsync(Guid fileId, Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<Guid> RegisterDeviceAsync(Guid deviceId, string name, CancellationToken cancellationToken = default);
    Task<Guid> EnsureDeviceRegisteredAsync(CancellationToken cancellationToken = default);
}

