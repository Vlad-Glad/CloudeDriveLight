namespace GDriveLight.Desktop.Services.Shell;

public sealed record VirtualFile(Guid Id, string Name, long SizeBytes, DateTime? CreatedAtUtc, DateTime? ModifiedAtUtc,
    Func<string, CancellationToken, Task> DownloadAsync);

public interface IVirtualFileDragOperation : IAsyncDisposable
{
    bool IsExternalTransfer { get; }
    Task CompleteAsync(CancellationToken cancellationToken);
}

public interface IVirtualFileDragService
{
    IVirtualFileDragOperation BeginDrag(VirtualFile file, string internalFormat, CancellationToken cancellationToken);
}
