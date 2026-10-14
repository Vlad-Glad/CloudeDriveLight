using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using GDriveLight.Desktop.Services.Shell;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class NativeVirtualFileTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NativeConsumerCanReadFromAnotherApartmentWithoutBlockingTheDragThreadsMessageLoop(int count)
    {
        var marshaled = new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finished = new ManualResetEventSlim();
        Exception? sourceFailure = null;
        var source = new Thread(() =>
        {
            try
            {
                Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
                try
                {
                    var file = new VirtualFile(Guid.NewGuid(), "notes.txt", 6, null, null,
                        (path, token) => File.WriteAllTextAsync(path, "abcdef", token));
                    var data = new VirtualFileDataObject(Enumerable.Range(0, count).Select(index =>
                        file with { Id = Guid.NewGuid(), Name = $"notes{index}.txt" }).ToArray(), "internal");
                    var pointer = Marshal.GetComInterfaceForObject(data, typeof(ComDataObject));
                    try
                    {
                        var id = typeof(ComDataObject).GUID;
                        Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(in id, pointer, out var packet));
                        marshaled.SetResult(packet);
                        // Deliberately do not pump messages: a Shell worker must not dispatch reads onto the UI thread.
                        finished.Wait(TimeSpan.FromSeconds(10));
                    }
                    finally
                    {
                        Marshal.Release(pointer);
                        data.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                finally { OleUninitialize(); }
            }
            catch (Exception exception) { sourceFailure = exception; marshaled.TrySetException(exception); }
        }) { IsBackground = true };
        source.SetApartmentState(ApartmentState.STA);
        source.Start();
        try
        {
            var packet = await marshaled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var read = Task.Run(() =>
            {
                Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 0));
                try
                {
                    var id = typeof(ComDataObject).GUID;
                    Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(packet, in id, out var dataPointer));
                    try
                    {
                        VerifyNativeFormats(dataPointer, count);
                        var capabilityId = typeof(IDataObjectAsyncCapability).GUID;
                        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(dataPointer, in capabilityId, out var capability));
                        var asyncMode = Marshal.GetDelegateForFunctionPointer<GetAsyncMode>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(capability), 4 * IntPtr.Size));
                        Marshal.ThrowExceptionForHR(asyncMode(capability, out var enabled));
                        Assert.Equal(1, enabled);
                        var start = Marshal.GetDelegateForFunctionPointer<StartOperation>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(capability), 5 * IntPtr.Size));
                        Marshal.ThrowExceptionForHR(start(capability, IntPtr.Zero));
                        var format = new FORMATETC
                        {
                            cfFormat = unchecked((short)DataFormats.GetDataFormat(VirtualFileDataObject.ContentsFormat).Id),
                            dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = count - 1, tymed = TYMED.TYMED_ISTREAM
                        };
                        var getData = Marshal.GetDelegateForFunctionPointer<GetData>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(dataPointer), 3 * IntPtr.Size));
                        Marshal.ThrowExceptionForHR(getData(dataPointer, ref format, out var medium));
                        try
                        {
                            var readStream = Marshal.GetDelegateForFunctionPointer<ReadStream>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(medium.unionmember), 3 * IntPtr.Size));
                            var buffer = Marshal.AllocCoTaskMem(6);
                            var bytesRead = Marshal.AllocCoTaskMem(4);
                            try
                            {
                                Assert.Equal(0, readStream(medium.unionmember, buffer, 4, bytesRead));
                                Assert.Equal(4, Marshal.ReadInt32(bytesRead));
                                var bytes = new byte[6];
                                Marshal.Copy(buffer, bytes, 0, 4);
                                Assert.Equal(1, readStream(medium.unionmember, buffer, 4, bytesRead));
                                Assert.Equal(2, Marshal.ReadInt32(bytesRead));
                                Marshal.Copy(buffer, bytes, 4, 2);
                                Assert.Equal(1, readStream(medium.unionmember, buffer, 4, bytesRead));
                                Assert.Equal(0, Marshal.ReadInt32(bytesRead));
                                return Encoding.UTF8.GetString(bytes);
                            }
                            finally { Marshal.FreeCoTaskMem(buffer); Marshal.FreeCoTaskMem(bytesRead); }
                        }
                        finally
                        {
                            ReleaseStgMedium(ref medium);
                            var end = Marshal.GetDelegateForFunctionPointer<EndOperation>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(capability), 7 * IntPtr.Size));
                            Marshal.ThrowExceptionForHR(end(capability, 0, IntPtr.Zero, 1));
                            Marshal.Release(capability);
                        }
                    }
                    finally { Marshal.Release(dataPointer); }
                }
                finally { CoUninitialize(); }
            });
            Assert.Equal("abcdef", await read.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            finished.Set();
            Assert.True(source.Join(TimeSpan.FromSeconds(5)));
        }
        Assert.Null(sourceFailure);
    }

    private static void VerifyNativeFormats(IntPtr data, int fileCount)
    {
        var enumerate = Marshal.GetDelegateForFunctionPointer<EnumFormats>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(data), 8 * IntPtr.Size));
        Marshal.ThrowExceptionForHR(enumerate(data, 1, out var enumerator));
        var size = Marshal.SizeOf<FORMATETC>();
        var count = 3 + fileCount;
        var buffer = Marshal.AllocCoTaskMem(count * size);
        try
        {
            var next = Marshal.GetDelegateForFunctionPointer<NextFormat>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(enumerator), 3 * IntPtr.Size));
            Assert.Equal(0, next(enumerator, count, buffer, out var fetched));
            Assert.Equal(count, fetched);
            var formats = Enumerable.Range(0, count).Select(index => Marshal.PtrToStructure<FORMATETC>(buffer + index * size)).ToArray();
            var contents = formats.Where(format => format.cfFormat == unchecked((short)DataFormats.GetDataFormat(VirtualFileDataObject.ContentsFormat).Id)).ToArray();
            Assert.Equal(Enumerable.Range(0, fileCount), contents.Select(format => format.lindex));
            Assert.All(contents, format => Assert.Equal(TYMED.TYMED_ISTREAM, format.tymed));
        }
        finally { Marshal.FreeCoTaskMem(buffer); Marshal.Release(enumerator); }
    }

    [Fact]
    public async Task WpfCanReadTheNativeInternalFormatForExistingCloudMoves()
    {
        var file = new VirtualFile(Guid.NewGuid(), "notes.txt", 0, null, null,
            (_, _) => throw new InvalidOperationException("Internal moves must not download."));
        await using var native = new VirtualFileDataObject(file, "GDriveLight.Desktop.ItemMove");
        var wpf = new DataObject(native);
        Assert.True(wpf.GetDataPresent("GDriveLight.Desktop.ItemMove", false));
        var value = Assert.IsType<MemoryStream>(wpf.GetData("GDriveLight.Desktop.ItemMove", false));
        Assert.Equal(file.Id.ToString("D"), Encoding.Unicode.GetString(value.ToArray()).TrimEnd('\0'));
        Assert.False(native.IsExternalTransfer);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetData(IntPtr instance, ref FORMATETC format, out STGMEDIUM medium);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReadStream(IntPtr instance, IntPtr buffer, int count, IntPtr bytesRead);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumFormats(IntPtr instance, int direction, out IntPtr enumerator);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NextFormat(IntPtr instance, int count, IntPtr formats, out int fetched);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAsyncMode(IntPtr instance, out int enabled);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StartOperation(IntPtr instance, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EndOperation(IntPtr instance, int result, IntPtr context, uint effects);
    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoMarshalInterThreadInterfaceInStream(in Guid id, IntPtr instance, out IntPtr stream);
    [DllImport("ole32.dll")] private static extern int CoGetInterfaceAndReleaseStream(IntPtr stream, in Guid id, out IntPtr instance);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
