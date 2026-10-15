using System.Buffers;
using System.Security.Cryptography;

namespace GDriveLight.Application.Files;

// Rewindable content plus a hash and the actual byte count, including non-seekable input.
internal sealed class PreparedFileContent(Stream stream, bool ownsStream, long sizeBytes, string hash) : IAsyncDisposable
{
    public Stream Stream { get; } = stream;
    public long SizeBytes { get; } = sizeBytes;
    public string Hash { get; } = hash;

    public static async Task<PreparedFileContent> CreateAsync(Stream source, CancellationToken token)
    {
        var ownsStream = !source.CanSeek;
        Stream content = source;
        if (ownsStream)
            content = new FileStream(Path.Combine(Path.GetTempPath(), $"gdrivelight-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        else
            source.Position = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                length = checked(length + read);
                hash.AppendData(buffer, 0, read);
                if (ownsStream) await content.WriteAsync(buffer.AsMemory(0, read), token);
            }
            content.Position = 0;
            return new PreparedFileContent(content, ownsStream, length, Convert.ToHexStringLower(hash.GetHashAndReset()));
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

    public ValueTask DisposeAsync() => ownsStream ? Stream.DisposeAsync() : ValueTask.CompletedTask;
}
