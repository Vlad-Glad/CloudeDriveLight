using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using GDriveLight.Desktop.Services.Shell;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class VirtualFileDataObjectTests
{
    private const string InternalFormat = "GDriveLight.Desktop.ItemMove";

    [Fact]
    public async Task DescriptorAndStreamProbingDoNotDownloadAndExposeUnicodeNameDatesAnd64BitSize()
    {
        var requested = 0;
        var created = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        var file = new VirtualFile(Guid.NewGuid(), "Звіт.cs", (1L << 32) + 7, created, created.AddHours(1), (_, _) =>
        {
            requested++;
            return Task.CompletedTask;
        });
        await using var data = new VirtualFileDataObject(file, InternalFormat);
        var descriptor = Format(VirtualFileDataObject.DescriptorFormat);
        data.GetData(ref descriptor, out var medium);
        try
        {
            var bytes = ReadGlobal(medium.unionmember, 596);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            Assert.Equal(created.ToFileTimeUtc(), BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(44)));
            Assert.Equal(created.AddHours(1).ToFileTimeUtc(), BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(60)));
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(68)));
            Assert.Equal(7u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(72)));
            Assert.Equal(file.Name, Encoding.Unicode.GetString(bytes, 76, 520).TrimEnd('\0'));
        }
        finally { ReleaseStgMedium(ref medium); }

        var preferred = Format(VirtualFileDataObject.PreferredEffectFormat);
        data.GetData(ref preferred, out medium);
        try { Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(ReadGlobal(medium.unionmember, 4))); }
        finally { ReleaseStgMedium(ref medium); }

        var contents = Format(VirtualFileDataObject.ContentsFormat, TYMED.TYMED_ISTREAM, 0);
        data.GetData(ref contents, out medium);
        try
        {
            var stream = Assert.IsAssignableFrom<IWindowsFileStream>(Marshal.GetObjectForIUnknown(medium.unionmember));
            stream.Stat(out var stat, 0);
            Assert.Equal(file.Name, stat.pwcsName);
            Assert.Equal(file.SizeBytes, stat.cbSize);
            stream.Seek(0, 2, IntPtr.Zero);
            Assert.Equal(0, requested);
        }
        finally { ReleaseStgMedium(ref medium); }

        var unknown = Format("GDriveLight.Tests.Unknown");
        Assert.True(data.QueryGetData(ref unknown) < 0);
        contents.lindex = 1;
        Assert.Equal(unchecked((int)0x80040068), data.QueryGetData(ref contents));
    }

    [Fact]
    public async Task RepeatedAndClonedReadsDownloadOnceAndCleanupWaitsForShellCompletion()
    {
        var requests = 0;
        string? cachedPath = null;
        var file = CreateFile(async (path, token) =>
        {
            Interlocked.Increment(ref requests);
            cachedPath = path;
            await File.WriteAllTextAsync(path, "abcdef", token);
        });
        var data = new VirtualFileDataObject(file, InternalFormat);
        data.StartOperation(IntPtr.Zero);
        var complete = data.CompleteAsync(CancellationToken.None);
        Assert.False(complete.IsCompleted);
        var format = Format(VirtualFileDataObject.ContentsFormat, TYMED.TYMED_ISTREAM, 0);
        data.GetData(ref format, out var medium);
        try
        {
            var stream = (IWindowsFileStream)Marshal.GetObjectForIUnknown(medium.unionmember);
            Assert.Equal("abc", Read(stream, 3));
            stream.Clone(out var clone);
            Assert.Equal("def", Read(clone, 3));
            Assert.Equal("def", Read(stream, 3));
            stream.Seek(0, 0, IntPtr.Zero);
            Assert.Equal("abcdef", Read(stream, 6));
            Assert.Equal(1, requests);
            Assert.True(File.Exists(cachedPath));
            Assert.False(complete.IsCompleted);
            data.EndOperation(0, IntPtr.Zero, 1);
            await complete;
            Assert.True(File.Exists(cachedPath));
        }
        finally
        {
            ReleaseStgMedium(ref medium);
            await data.DisposeAsync();
        }
        Assert.False(Directory.Exists(Path.GetDirectoryName(cachedPath)));
    }

    [Fact]
    public async Task CancelingTransferStopsTheDownloadAndRemovesPartialContent()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? cachedPath = null;
        var file = CreateFile(async (path, token) =>
        {
            cachedPath = path;
            await File.WriteAllTextAsync(path, "partial", token);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        using var cancellation = new CancellationTokenSource();
        var data = new VirtualFileDataObject(file, InternalFormat, cancellation.Token);
        data.StartOperation(IntPtr.Zero);
        var completion = data.CompleteAsync(cancellation.Token);
        var format = Format(VirtualFileDataObject.ContentsFormat, TYMED.TYMED_ISTREAM, 0);
        data.GetData(ref format, out var medium);
        var stream = (IWindowsFileStream)Marshal.GetObjectForIUnknown(medium.unionmember);
        var read = Task.Run(() => Read(stream, 6));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
        }
        finally
        {
            ReleaseStgMedium(ref medium);
            await data.DisposeAsync();
        }
        Assert.False(Directory.Exists(Path.GetDirectoryName(cachedPath)));
    }

    [Fact]
    public async Task FailedDownloadIsReportedInsteadOfReturningATruncatedFile()
    {
        string? cachedPath = null;
        var file = CreateFile(async (path, token) =>
        {
            cachedPath = path;
            await File.WriteAllTextAsync(path, "abc", token); // Descriptor promises six bytes.
        });
        var data = new VirtualFileDataObject(file, InternalFormat);
        var format = Format(VirtualFileDataObject.ContentsFormat, TYMED.TYMED_ISTREAM, 0);
        data.GetData(ref format, out var medium);
        try
        {
            var stream = (IWindowsFileStream)Marshal.GetObjectForIUnknown(medium.unionmember);
            Assert.Throws<IOException>(() => Read(stream, 6));
            await Assert.ThrowsAsync<IOException>(() => data.CompleteAsync(CancellationToken.None));
        }
        finally
        {
            ReleaseStgMedium(ref medium);
            await data.DisposeAsync();
        }
        Assert.False(Directory.Exists(Path.GetDirectoryName(cachedPath)));
    }

    [Fact]
    public async Task NativeObjectExposesAsyncCapabilityAndInternalIdWithoutRequestingContent()
    {
        var file = CreateFile((_, _) => throw new InvalidOperationException("Content was not requested."));
        await using var data = new VirtualFileDataObject(file, InternalFormat);
        var pointer = Marshal.GetComInterfaceForObject(data, typeof(ComDataObject));
        var capabilityId = typeof(IDataObjectAsyncCapability).GUID;
        try
        {
            Assert.Equal(0, Marshal.QueryInterface(pointer, in capabilityId, out var capability));
            Marshal.Release(capability);
        }
        finally { Marshal.Release(pointer); }

        var format = Format(InternalFormat);
        data.GetData(ref format, out var medium);
        try { Assert.Equal(file.Id.ToString("D"), Encoding.Unicode.GetString(ReadGlobal(medium.unionmember, 74)).TrimEnd('\0')); }
        finally { ReleaseStgMedium(ref medium); }
        Assert.False(data.IsExternalTransfer);
        data.GetAsyncMode(out var asyncMode);
        Assert.True(asyncMode);
    }

    private static VirtualFile CreateFile(Func<string, CancellationToken, Task> download) =>
        new(Guid.NewGuid(), "notes.txt", 6, DateTime.UtcNow, null, download);

    private static FORMATETC Format(string name, TYMED medium = TYMED.TYMED_HGLOBAL, int index = -1) => new()
    {
        cfFormat = unchecked((short)DataFormats.GetDataFormat(name).Id), dwAspect = DVASPECT.DVASPECT_CONTENT,
        tymed = medium, lindex = index
    };

    private static string Read(IWindowsFileStream stream, int count)
    {
        var buffer = new byte[count];
        var pointer = Marshal.AllocCoTaskMem(4);
        try
        {
            Marshal.ThrowExceptionForHR(stream.Read(buffer, count, pointer));
            return Encoding.UTF8.GetString(buffer, 0, Marshal.ReadInt32(pointer));
        }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }

    private static byte[] ReadGlobal(IntPtr handle, int length)
    {
        var pointer = GlobalLock(handle);
        try { var bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length); return bytes; }
        finally { GlobalUnlock(handle); }
    }

    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
