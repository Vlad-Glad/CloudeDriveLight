using System.Security.Cryptography;
using GDriveLight.Application.Common.Exceptions;
using GDriveLight.Infrastructure.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GDriveLight.Tests.Infrastructure;

public sealed class FileContentPreparerTests : IDisposable
{
    private readonly string _temporaryPath = Path.Combine(Path.GetTempPath(), $"gdrivelight-preparer-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 3)]
    public async Task PreparationCountsAndHashesActualBytesAndPreservesSourceOwnership(bool nonSeekable, int size)
    {
        var bytes = Enumerable.Range(1, size).Select(value => (byte)value).ToArray();
        using Stream source = nonSeekable ? new NonSeekableStream(bytes) : new MemoryStream(bytes);
        if (source.CanSeek) source.Position = size;

        await using (var prepared = await Create().PrepareAsync(source))
        {
            Assert.Equal(size, prepared.SizeBytes);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), prepared.Hash);
            Assert.Equal(0, prepared.Stream.Position);
            using var copy = new MemoryStream();
            await prepared.Stream.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
            if (nonSeekable) Assert.Single(Directory.GetFiles(_temporaryPath));
        }

        Assert.True(source.CanRead);
        AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedContentIsRejectedUsingActualBytesAndCleansUp(bool nonSeekable)
    {
        using Stream source = nonSeekable ? new NonSeekableStream(new byte[100]) : new MisreportedLengthStream(new byte[100]);

        var error = await Assert.ThrowsAsync<FileSizeLimitExceededException>(() => Create().PrepareAsync(source));

        Assert.Equal(3, error.MaxFileSizeBytes);
        Assert.True(source.CanRead);
        if (source is NonSeekableStream input) Assert.Equal(4, input.BytesRead);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ReadFailureRemovesPartialTemporaryFileAndLeavesSourceOpen()
    {
        using var source = new NonSeekableStream(new byte[4]) { FailAfterFirstRead = true };

        await Assert.ThrowsAsync<IOException>(() => Create(10).PrepareAsync(source));

        Assert.True(source.CanRead);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CancellationDuringPreparationRemovesTemporaryFileAndLeavesSourceOpen()
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new NonSeekableStream(new byte[4]) { AfterRead = cancellation.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(10).PrepareAsync(source, cancellation.Token));

        Assert.True(source.CanRead);
        AssertNoTemporaryFiles();
    }

    private FileContentPreparer Create(long maxFileSizeBytes = 3) => new(Options.Create(new FileContentPreparationOptions
    {
        MaxFileSizeBytes = maxFileSizeBytes,
        TemporaryPath = _temporaryPath
    }));

    private void AssertNoTemporaryFiles()
    {
        if (Directory.Exists(_temporaryPath)) Assert.Empty(Directory.GetFiles(_temporaryPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryPath)) Directory.Delete(_temporaryPath, true);
    }

    private sealed class MisreportedLengthStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override long Length => 0;
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public bool FailAfterFirstRead { get; init; }
        public Action? AfterRead { get; init; }
        public int BytesRead { get; private set; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailAfterFirstRead && BytesRead > 0) throw new IOException("Source read failed.");
            var read = _inner.Read(buffer.Span[..Math.Min(buffer.Length, 2)]);
            BytesRead += read;
            AfterRead?.Invoke();
            return ValueTask.FromResult(read);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
