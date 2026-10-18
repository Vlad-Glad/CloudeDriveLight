namespace GDriveLight.Application.Files;

public sealed class PreparedFileContent(Stream stream, long sizeBytes, string hash, bool ownsStream) : IAsyncDisposable
{
    public Stream Stream { get; } = stream;
    public long SizeBytes { get; } = sizeBytes;
    public string Hash { get; } = hash;

    public ValueTask DisposeAsync() => ownsStream ? Stream.DisposeAsync() : ValueTask.CompletedTask;
}
