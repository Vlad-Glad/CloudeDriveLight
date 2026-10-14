using GDriveLight.Desktop.Services.Shell;

namespace GDriveLight.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly IVirtualFileDragService _virtualFileDrag;

    public async Task DragFileAsync(FileItemViewModel item, string internalFormat)
    {
        if (!CanActOn(item) || item.IsFolder) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_connectionLifetime.Token);
        try
        {
            var file = new VirtualFile(item.Id, item.Name, item.SizeBytes, item.CreatedAtUtc, item.ModifiedAtUtc ?? item.CreatedAtUtc,
                (path, token) => _apiService.DownloadFileAsync(item.Id, path, token));
            await using var transfer = _virtualFileDrag.BeginDrag(file, internalFormat, cancellation.Token);
            if (!transfer.IsExternalTransfer) return;

            await RunOperationAsync($"Downloading '{item.Name}'...", async token =>
            {
                using var registration = token.Register(cancellation.Cancel);
                try { await transfer.CompleteAsync(token); }
                finally
                {
                    // Cancellation may complete the wait before its other registrations run.
                    if (token.IsCancellationRequested) cancellation.Cancel();
                }
                return $"Copied '{item.Name}' to the destination.";
            }, refreshAfter: false);
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) { HandleOperationError(exception); }
    }
}
