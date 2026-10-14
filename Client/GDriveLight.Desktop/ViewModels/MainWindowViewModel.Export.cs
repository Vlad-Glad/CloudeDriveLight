using GDriveLight.Desktop.Services.Shell;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly IVirtualFileDragService _virtualFileDrag;

    public Task DragFileAsync(FileItemViewModel item, string internalFormat) => DragFilesAsync(new[] { item }, internalFormat);

    public async Task DragFilesAsync(IReadOnlyList<FileItemViewModel> selection, string internalFormat)
    {
        var items = selection.DistinctBy(item => item.Id).ToArray();
        if (items.Length == 0 || items.Any(item => !CanActOn(item) || item.IsFolder)) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_connectionLifetime.Token);
        try
        {
            var files = items.Select(item => new VirtualFile(item.Id, item.Name, item.SizeBytes, item.CreatedAtUtc, item.ModifiedAtUtc ?? item.CreatedAtUtc,
                (path, token) => _apiService.DownloadFileAsync(item.Id, path, token))).ToArray();
            await using var transfer = _virtualFileDrag.BeginDrag(files, internalFormat, cancellation.Token);
            if (!transfer.IsExternalTransfer) return;

            await RunOperationAsync($"Downloading {items.Length} file(s)...", async token =>
            {
                using var registration = token.Register(cancellation.Cancel);
                try { await transfer.CompleteAsync(token); }
                finally
                {
                    // Cancellation may complete the wait before its other registrations run.
                    if (token.IsCancellationRequested) cancellation.Cancel();
                }
                return $"Copied {items.Length} file(s) to the destination.";
            }, refreshAfter: false);
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) { HandleOperationError(exception); }
    }
}
