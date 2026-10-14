using System.IO;

namespace GDriveLight.Desktop.Services.Shell;

internal sealed class VirtualFileContent(VirtualFile file, CancellationToken cancellationToken) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    private readonly List<VirtualFileStream> _streams = new();
    private Task<string>? _download;
    private string? _directory;
    private bool _disposed;

    internal VirtualFile Metadata => file;
    internal CancellationToken Token => _lifetime.Token;

    internal VirtualFileStream CreateStream(long position = 0)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var stream = new VirtualFileStream(this, position);
            _streams.Add(stream);
            return stream;
        }
    }

    internal FileStream OpenRead()
    {
        Task<string> download;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            download = _download ??= Task.Run(async () =>
            {
                Token.ThrowIfCancellationRequested();
                _directory = Path.Combine(Path.GetTempPath(), $"gdrivelight-drag-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, "content");
                await file.DownloadAsync(path, Token).ConfigureAwait(false);
                Token.ThrowIfCancellationRequested();
                if (new FileInfo(path).Length != file.SizeBytes)
                    throw new IOException("The file changed during the transfer. Refresh and try again.");
                return path;
            });
        }

        var downloadedPath = download.GetAwaiter().GetResult();
        Token.ThrowIfCancellationRequested();
        return new FileStream(downloadedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    internal void ThrowIfFailed()
    {
        Task<string>? download;
        lock (_gate) download = _download;
        if (download is { IsCompleted: true }) download.GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        Task<string>? download;
        VirtualFileStream[] streams;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            download = _download;
            streams = _streams.ToArray();
        }
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (download is not null)
        {
            try { await download.ConfigureAwait(false); }
            catch { /* Transfer errors are reported by the operation; cleanup must still run. */ }
        }
        foreach (var stream in streams) stream.Dispose();
        if (_directory is not null && Directory.Exists(_directory))
        {
            File.Delete(Path.Combine(_directory, "content"));
            Directory.Delete(_directory);
        }
        _lifetime.Dispose();
    }
}
