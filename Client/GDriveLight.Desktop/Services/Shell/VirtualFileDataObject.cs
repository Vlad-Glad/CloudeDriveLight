using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GDriveLight.Desktop.Services.Shell;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class VirtualFileDataObject : IDataObject, IDataObjectAsyncCapability, IAsyncDisposable
{
    public const string DescriptorFormat = "FileGroupDescriptorW";
    public const string ContentsFormat = "FileContents";
    public const string PreferredEffectFormat = "Preferred DropEffect";
    private readonly VirtualFileContent _content;
    private readonly Dictionary<short, byte[]> _metadata;
    private readonly short _contentsFormat = ShellNative.RegisterFormat(ContentsFormat);
    private readonly object _gate = new();
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _asyncMode = true;
    private bool _inOperation;
    private bool _operationStarted;
    private bool _contentsRequested;

    public VirtualFileDataObject(VirtualFile file, string internalFormat, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(file.Name) || file.Name is "." or ".." || file.Name.Length >= 260 ||
            file.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.SizeBytes < 0)
            throw new ArgumentException("The file name or size is not valid for Windows.", nameof(file));
        _content = new VirtualFileContent(file, cancellationToken);
        _metadata = new Dictionary<short, byte[]>
        {
            [ShellNative.RegisterFormat(DescriptorFormat)] = CreateDescriptor(file),
            [ShellNative.RegisterFormat(PreferredEffectFormat)] = BitConverter.GetBytes(1), // DROPEFFECT_COPY
            [ShellNative.RegisterFormat(internalFormat)] = Encoding.Unicode.GetBytes(file.Id.ToString("D") + '\0')
        };
    }

    public bool IsExternalTransfer { get { lock (_gate) return _contentsRequested || _operationStarted; } }

    public async Task CompleteAsync(CancellationToken token)
    {
        bool pending;
        lock (_gate) pending = _inOperation;
        try
        {
            if (pending) await _finished.Task.WaitAsync(token).ConfigureAwait(false);
            else if (_finished.Task.IsCompleted) await _finished.Task.ConfigureAwait(false);
        }
        catch { _content.ThrowIfFailed(); throw; }
        token.ThrowIfCancellationRequested();
        _content.ThrowIfFailed();
    }

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        Marshal.ThrowExceptionForHR(QueryGetData(ref format));
        if (format.cfFormat == _contentsFormat)
        {
            lock (_gate) _contentsRequested = true;
            var stream = _content.CreateStream();
            medium = new STGMEDIUM { tymed = TYMED.TYMED_ISTREAM, unionmember = Marshal.GetComInterfaceForObject(stream, typeof(IWindowsFileStream)) };
            return;
        }

        byte[] bytes;
        lock (_gate) bytes = _metadata[format.cfFormat];
        medium = ShellNative.CreateGlobal(bytes);
    }

    public int QueryGetData(ref FORMATETC format)
    {
        if (format.dwAspect != DVASPECT.DVASPECT_CONTENT) return ShellNative.AspectError;
        if (format.cfFormat == _contentsFormat)
        {
            if (format.lindex != 0) return ShellNative.IndexError;
            return (format.tymed & TYMED.TYMED_ISTREAM) != 0 ? 0 : ShellNative.MediumError;
        }
        lock (_gate)
        {
            if (!_metadata.ContainsKey(format.cfFormat)) return ShellNative.FormatError;
        }
        return (format.tymed & TYMED.TYMED_HGLOBAL) != 0 ? 0 : ShellNative.MediumError;
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if (direction != DATADIR.DATADIR_GET) throw new COMException("Only reading is supported.", ShellNative.NotImplemented);
        lock (_gate)
        {
            return new FormatEnumerator(_metadata.Keys.Select(id => new FORMATETC
            {
                cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL
            }).Append(new FORMATETC
            {
                cfFormat = _contentsFormat, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = 0, tymed = TYMED.TYMED_ISTREAM
            }).ToArray());
        }
    }

    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release)
    {
        if (medium.tymed != TYMED.TYMED_HGLOBAL) throw new COMException("Unsupported transfer medium.", ShellNative.MediumError);
        // Shell completion notifications never authorize deleting the cloud original.
        var bytes = ShellNative.ReadGlobal(medium.unionmember);
        lock (_gate) _metadata[format.cfFormat] = bytes;
        if (release) ShellNative.ReleaseStgMedium(ref medium);
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Operation is not supported.", ShellNative.NotImplemented);
    public int GetCanonicalFormatEtc(ref FORMATETC input, out FORMATETC output)
    {
        output = input;
        output.ptd = IntPtr.Zero;
        return 0x40130; // DATA_S_SAMEFORMATETC
    }
    public int DAdvise(ref FORMATETC format, ADVF flags, IAdviseSink sink, out int connection)
    {
        connection = 0;
        return unchecked((int)0x80040003); // OLE_E_ADVISENOTSUPPORTED
    }
    public void DUnadvise(int connection) => throw new COMException("Advisory connections are not supported.", unchecked((int)0x80040003));
    public int EnumDAdvise(out IEnumSTATDATA? connections) { connections = null; return unchecked((int)0x80040003); }

    public int SetAsyncMode(bool asyncMode) { lock (_gate) _asyncMode = asyncMode; return 0; }
    public int GetAsyncMode(out bool asyncMode) { lock (_gate) asyncMode = _asyncMode; return 0; }
    public int StartOperation(IntPtr bindContext) { lock (_gate) { _inOperation = true; _operationStarted = true; } return 0; }
    public int InOperation(out bool inOperation) { lock (_gate) inOperation = _inOperation; return 0; }
    public int EndOperation(int result, IntPtr bindContext, uint effects)
    {
        lock (_gate)
        {
            _inOperation = false;
            if (result < 0) _finished.TrySetException(Marshal.GetExceptionForHR(result) ?? new IOException("The file transfer failed."));
            else _finished.TrySetResult();
        }
        return 0;
    }

    public ValueTask DisposeAsync() => _content.DisposeAsync();

    private static byte[] CreateDescriptor(VirtualFile file)
    {
        var bytes = new byte[4 + 592]; // FILEGROUPDESCRIPTORW with one FILEDESCRIPTORW
        using var writer = new BinaryWriter(new MemoryStream(bytes));
        uint flags = 0x80004044; // FD_UNICODE | FD_PROGRESSUI | FD_FILESIZE | FD_ATTRIBUTES
        if (file.CreatedAtUtc.HasValue) flags |= 0x8;
        if (file.ModifiedAtUtc.HasValue) flags |= 0x20;
        writer.Write(1u);
        writer.Write(flags);
        writer.Write(new byte[32]); // CLSID, SIZEL, POINTL
        writer.Write(0x80u); // FILE_ATTRIBUTE_NORMAL
        writer.Write(file.CreatedAtUtc?.ToFileTimeUtc() ?? 0);
        writer.Write(0L);
        writer.Write(file.ModifiedAtUtc?.ToFileTimeUtc() ?? 0);
        writer.Write((uint)(file.SizeBytes >> 32));
        writer.Write((uint)file.SizeBytes);
        writer.Write(Encoding.Unicode.GetBytes(file.Name));
        return bytes;
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class FormatEnumerator(FORMATETC[] formats, int position = 0) : IEnumFORMATETC
    {
        private int _position = position;
        public int Next(int count, FORMATETC[] result, int[]? fetched)
        {
            var copied = Math.Min(count, formats.Length - _position);
            Array.Copy(formats, _position, result, 0, copied);
            _position += copied;
            if (fetched is not null) fetched[0] = copied;
            return copied == count ? 0 : 1;
        }
        public int Skip(int count) { var skipped = Math.Min(count, formats.Length - _position); _position += skipped; return skipped == count ? 0 : 1; }
        public int Reset() { _position = 0; return 0; }
        public void Clone(out IEnumFORMATETC result) => result = new FormatEnumerator(formats, _position);
    }
}
