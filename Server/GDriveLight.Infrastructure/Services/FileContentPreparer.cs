using System.Buffers;
using System.Security.Cryptography;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Exceptions;
using GDriveLight.Application.Files;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GDriveLight.Infrastructure.Services;

public sealed class FileContentPreparer(IOptions<FileContentPreparationOptions> options) : IFileContentPreparer
{
    public async Task<PreparedFileContent> PrepareAsync(Stream source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        var ownsStream = !source.CanSeek;
        Stream content = source;
        if (ownsStream)
        {
            Directory.CreateDirectory(settings.TemporaryPath);
            content = new FileStream(Path.Combine(settings.TemporaryPath, $"gdrivelight-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
        else
            source.Position = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            while (true)
            {
                // Read at most one byte beyond the limit, even when Length is unavailable or unreliable.
                var remaining = settings.MaxFileSizeBytes - length;
                var count = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                var read = await source.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
                if (read == 0) break;
                length = checked(length + read);
                if (length > settings.MaxFileSizeBytes)
                    throw new FileSizeLimitExceededException(settings.MaxFileSizeBytes);
                hash.AppendData(buffer, 0, read);
                if (ownsStream) await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            content.Position = 0;
            return new PreparedFileContent(content, length, Convert.ToHexStringLower(hash.GetHashAndReset()), ownsStream);
        }
        catch
        {
            if (ownsStream) await content.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
