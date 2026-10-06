namespace GDriveLight.Application.Abstractions.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves the file stream and returns the stored file path/URL.
    /// </summary>
    Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the file at the given path/URL.
    /// </summary>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a stream to read the file at the given path/URL.
    /// </summary>
    Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default);
}