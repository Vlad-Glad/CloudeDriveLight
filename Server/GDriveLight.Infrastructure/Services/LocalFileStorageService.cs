using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GDriveLight.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;

    public LocalFileStorageService(IOptions<LocalFileStorageOptions> options)
    {
        var directory = options.Value.Path;

        _storageDirectory = Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(Directory.GetCurrentDirectory(), directory);

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var filePath = Path.Combine(_storageDirectory, uniqueFileName);

        var created = false;
        try
        {
            await using var fileStreamToWrite = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            created = true;
            await fileStream.CopyToAsync(fileStreamToWrite, cancellationToken);
            return uniqueFileName;
        }
        catch
        {
            if (created) File.Delete(filePath);
            throw;
        }
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileUrl);
        var filePath = Path.Combine(_storageDirectory, safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileUrl);
        var filePath = Path.Combine(_storageDirectory, safeFileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {fileUrl}");
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }
}
