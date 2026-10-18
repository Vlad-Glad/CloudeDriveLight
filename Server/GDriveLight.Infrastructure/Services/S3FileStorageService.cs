using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GDriveLight.Infrastructure.Services;

public sealed class S3FileStorageService(
    IAmazonS3 client,
    IOptions<S3StorageOptions> options,
    ILogger<S3FileStorageService> logger) : IFileStorageService
{
    private readonly string _bucketName = options.Value.BucketName;
    private readonly bool _useChunkEncoding = string.IsNullOrWhiteSpace(options.Value.ServiceUrl);

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Original names remain in metadata. Opaque keys avoid path, length and encoding issues in storage.
        var key = Guid.NewGuid().ToString("N");
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                UseChunkEncoding = _useChunkEncoding,
                ContentType = "application/octet-stream"
            }, cancellationToken);
            return key;
        }
        catch
        {
            // A lost response can leave a complete object in S3. Make a bounded cleanup attempt.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await DeleteAsync(key, cleanup.Token);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not remove failed S3 upload {StorageKey}", key);
            }
            throw;
        }
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileUrl);
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucketName, Key = fileUrl }, cancellationToken);
        }
        catch (AmazonS3Exception error) when (IsMissingObject(error))
        {
            // Deletion is idempotent, matching local storage.
        }
    }

    public async Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileUrl);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = _bucketName, Key = fileUrl }, cancellationToken);
            return new S3ObjectReadStream(response);
        }
        catch (AmazonS3Exception error) when (IsMissingObject(error))
        {
            throw new FileNotFoundException("Stored file was not found.", fileUrl, error);
        }
    }

    private static bool IsMissingObject(AmazonS3Exception error) => error.StatusCode == HttpStatusCode.NotFound &&
        (string.IsNullOrEmpty(error.ErrorCode) || error.ErrorCode is "NoSuchKey" or "NotFound");

    // ASP.NET disposes the returned stream. Keep the SDK response alive until that point and release both together.
    private sealed class S3ObjectReadStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _stream = response.ResponseStream;
        private bool _disposed;
        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _stream.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
