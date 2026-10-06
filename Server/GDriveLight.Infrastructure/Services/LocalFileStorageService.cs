using GDriveLight.Application.Abstractions.Services;

namespace GDriveLight.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;

    public LocalFileStorageService()
    {
        // For development, we store files in an 'uploads' directory relative to the current working directory
        _storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(_storageDirectory, uniqueFileName);

        using var fileStreamToWrite = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await fileStream.CopyToAsync(fileStreamToWrite, cancellationToken);

        return uniqueFileName;
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storageDirectory, fileUrl);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storageDirectory, fileUrl);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {fileUrl}");
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }
}
