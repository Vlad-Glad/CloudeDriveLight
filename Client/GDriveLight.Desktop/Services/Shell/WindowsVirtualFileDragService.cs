using System.Runtime.InteropServices;

namespace GDriveLight.Desktop.Services.Shell;

public sealed class WindowsVirtualFileDragService : IVirtualFileDragService
{
    public IVirtualFileDragOperation BeginDrag(IReadOnlyList<VirtualFile> files, string internalFormat, CancellationToken cancellationToken)
    {
        var data = new VirtualFileDataObject(files, internalFormat, cancellationToken);
        try
        {
            Marshal.ThrowExceptionForHR(ShellNative.OleInitialize(IntPtr.Zero));
            try
            {
                // Use native OLE: WPF's DataObject wrapper would hide IDataObjectAsyncCapability.
                var result = ShellNative.DoDragDrop(data, new DropSource(cancellationToken), 3, out _);
                Marshal.ThrowExceptionForHR(result);
                return new Operation(data, result != ShellNative.DragCancelled);
            }
            finally { ShellNative.OleUninitialize(); }
        }
        catch (Exception exception) { return new Operation(data, false, exception); }
    }

    private sealed class Operation(VirtualFileDataObject data, bool dropped, Exception? failure = null) : IVirtualFileDragOperation
    {
        public bool IsExternalTransfer => failure is not null || dropped && data.IsExternalTransfer;
        public Task CompleteAsync(CancellationToken cancellationToken) => failure is null
            ? data.CompleteAsync(cancellationToken) : Task.FromException(failure);
        public ValueTask DisposeAsync() => data.DisposeAsync();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class DropSource(CancellationToken token) : IOleDropSource
    {
        public int QueryContinueDrag(bool escapePressed, uint keyState) => escapePressed || token.IsCancellationRequested
            ? ShellNative.DragCancelled : (keyState & 1) == 0 ? 0x40100 : 0;
        public int GiveFeedback(uint effect) => 0x40102;
    }
}
