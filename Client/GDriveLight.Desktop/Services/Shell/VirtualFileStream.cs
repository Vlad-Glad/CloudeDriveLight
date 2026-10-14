using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace GDriveLight.Desktop.Services.Shell;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class VirtualFileStream : IWindowsFileStream, IDisposable
{
    private readonly VirtualFileContent _content;
    private readonly object _gate = new();
    private FileStream? _stream;
    private long _position;
    private bool _disposed;

    internal VirtualFileStream(VirtualFileContent content, long position)
    {
        _content = content;
        _position = position;
    }

    public int Read(byte[] buffer, int count, IntPtr bytesRead)
    {
        if (bytesRead != IntPtr.Zero) Marshal.WriteInt32(bytesRead, 0);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _content.Token.ThrowIfCancellationRequested();
            if (count == 0) return 0;
            _stream ??= _content.OpenRead();
            _stream.Position = _position;
            var read = _stream.Read(buffer, 0, count);
            _position += read;
            if (bytesRead != IntPtr.Zero) Marshal.WriteInt32(bytesRead, read);
            return read == count ? 0 : 1;
        }
    }

    public void Seek(long offset, int origin, IntPtr newPosition)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var position = checked(origin switch
            {
                0 => offset,
                1 => _position + offset,
                2 => _content.Metadata.SizeBytes + offset,
                _ => throw new COMException("Invalid seek origin.", unchecked((int)0x80030001))
            });
            if (position < 0) throw new COMException("Invalid stream position.", unchecked((int)0x80030019));
            _position = position;
            if (newPosition != IntPtr.Zero) Marshal.WriteInt64(newPosition, position);
        }
    }

    public void Stat(out STATSTG stat, int flags) => stat = new STATSTG
    {
        type = 2, cbSize = _content.Metadata.SizeBytes,
        pwcsName = (flags & 1) == 0 ? _content.Metadata.Name : null!
    };

    public void Clone(out IWindowsFileStream stream)
    {
        lock (_gate) stream = _content.CreateStream(_position);
    }

    public void CopyTo(IStream destination, long count, IntPtr bytesRead, IntPtr bytesWritten)
    {
        var buffer = new byte[65536];
        var readPointer = Marshal.AllocCoTaskMem(sizeof(int));
        var writtenPointer = Marshal.AllocCoTaskMem(sizeof(int));
        long totalRead = 0, totalWritten = 0;
        try
        {
            while (totalRead < count)
            {
                Read(buffer, (int)Math.Min(buffer.Length, count - totalRead), readPointer);
                var read = Marshal.ReadInt32(readPointer);
                if (read == 0) break;
                destination.Write(buffer, read, writtenPointer);
                var written = Marshal.ReadInt32(writtenPointer);
                totalRead += read;
                totalWritten += written;
                if (written != read) throw new COMException("The destination could not write the file.", unchecked((int)0x8003001D));
            }
        }
        finally
        {
            if (bytesRead != IntPtr.Zero) Marshal.WriteInt64(bytesRead, totalRead);
            if (bytesWritten != IntPtr.Zero) Marshal.WriteInt64(bytesWritten, totalWritten);
            Marshal.FreeCoTaskMem(readPointer);
            Marshal.FreeCoTaskMem(writtenPointer);
        }
    }

    public void Write(byte[] buffer, int count, IntPtr bytesWritten) => throw new COMException("The file is read-only.", ShellNative.AccessDenied);
    public void SetSize(long size) => throw new COMException("The file is read-only.", ShellNative.AccessDenied);
    public void Commit(int flags) { }
    public void Revert() => throw new COMException("Operation is not supported.", ShellNative.NotImplemented);
    public void LockRegion(long offset, long count, int type) => throw new COMException("Operation is not supported.", ShellNative.NotImplemented);
    public void UnlockRegion(long offset, long count, int type) => throw new COMException("Operation is not supported.", ShellNative.NotImplemented);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _stream?.Dispose();
        }
    }
}
