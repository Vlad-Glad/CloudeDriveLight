using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace GDriveLight.Desktop.Services.Shell;

[ComVisible(true), Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDataObjectAsyncCapability
{
    [PreserveSig] int SetAsyncMode([MarshalAs(UnmanagedType.Bool)] bool asyncMode);
    [PreserveSig] int GetAsyncMode([MarshalAs(UnmanagedType.Bool)] out bool asyncMode);
    [PreserveSig] int StartOperation(IntPtr bindContext);
    [PreserveSig] int InOperation([MarshalAs(UnmanagedType.Bool)] out bool inOperation);
    [PreserveSig] int EndOperation(int result, IntPtr bindContext, uint effects);
}

[ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, uint keyState);
    [PreserveSig] int GiveFeedback(uint effect);
}

[ComVisible(true), Guid("0000000C-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IWindowsFileStream
{
    // Preserve the HRESULT so a short read can return S_FALSE, as required by IStream.
    [PreserveSig] int Read([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer, int count, IntPtr bytesRead);
    void Write([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer, int count, IntPtr bytesWritten);
    void Seek(long offset, int origin, IntPtr newPosition);
    void SetSize(long size);
    void CopyTo(IStream destination, long count, IntPtr bytesRead, IntPtr bytesWritten);
    void Commit(int flags);
    void Revert();
    void LockRegion(long offset, long count, int type);
    void UnlockRegion(long offset, long count, int type);
    void Stat(out STATSTG stat, int flags);
    void Clone(out IWindowsFileStream stream);
}

internal static class ShellNative
{
    internal const int FormatError = unchecked((int)0x80040064);
    internal const int IndexError = unchecked((int)0x80040068);
    internal const int MediumError = unchecked((int)0x80040069);
    internal const int AspectError = unchecked((int)0x8004006B);
    internal const int NotImplemented = unchecked((int)0x80004001);
    internal const int AccessDenied = unchecked((int)0x80030005);
    internal const int DragCancelled = 0x40101;

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    internal static extern int DoDragDrop(IDataObject data, IOleDropSource source, uint allowedEffects, out uint effect);

    [DllImport("ole32.dll")]
    internal static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern nuint GlobalSize(IntPtr handle);

    internal static short RegisterFormat(string name)
    {
        var format = RegisterClipboardFormat(name);
        if (format == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return unchecked((short)format);
    }

    internal static STGMEDIUM CreateGlobal(byte[] bytes)
    {
        var handle = GlobalAlloc(0x42, (nuint)Math.Max(1, bytes.Length)); // GMEM_MOVEABLE | GMEM_ZEROINIT
        if (handle == IntPtr.Zero) throw new OutOfMemoryException();
        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        try
        {
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
            finally { GlobalUnlock(handle); }
        }
        catch { GlobalFree(handle); throw; }
        return new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = handle };
    }

    internal static byte[] ReadGlobal(IntPtr handle)
    {
        var length = checked((int)GlobalSize(handle));
        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero) throw new COMException("Could not read transfer metadata.", MediumError);
        try
        {
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return bytes;
        }
        finally { GlobalUnlock(handle); }
    }
}
