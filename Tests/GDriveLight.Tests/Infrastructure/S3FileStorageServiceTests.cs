using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using GDriveLight.Infrastructure.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GDriveLight.Tests.Infrastructure;

public sealed class S3FileStorageServiceTests
{
    private const string Bucket = "private-test-files";
    private readonly Mock<IAmazonS3> _client = new(MockBehavior.Strict);

    [Fact]
    public async Task SaveUsesUniqueOpaqueKeysConfiguredBucketAndLeavesInputOpen()
    {
        var requests = new List<PutObjectRequest>();
        using var cancellation = new CancellationTokenSource();
        using var input = new MemoryStream("private content"u8.ToArray());
        input.Position = 2;
        _client.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), cancellation.Token))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(new PutObjectResponse());
        var storage = Create();

        var first = await storage.SaveAsync(input, "../../private.txt", cancellation.Token);
        var second = await storage.SaveAsync(input, "private.txt", cancellation.Token);

        Assert.NotEqual(first, second);
        Assert.True(Guid.TryParseExact(first, "N", out _));
        Assert.True(Guid.TryParseExact(second, "N", out _));
        Assert.Equal(new[] { first, second }, requests.Select(request => request.Key));
        Assert.All(requests, request =>
        {
            Assert.Equal(Bucket, request.BucketName);
            Assert.Same(input, request.InputStream);
            Assert.False(request.AutoCloseStream);
            Assert.False(request.AutoResetStreamPosition);
        });
        Assert.True(input.CanRead);
        Assert.Equal(2, input.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetStreamsContentAndDisposalReleasesSdkResponse(bool asyncDispose)
    {
        using var cancellation = new CancellationTokenSource();
        var source = new TrackingStream("downloaded content"u8.ToArray());
        _client.Setup(client => client.GetObjectAsync(
            It.Is<GetObjectRequest>(request => request.BucketName == Bucket && request.Key == "folder/exact-key"), cancellation.Token))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = source });

        var stream = await Create().GetAsync("folder/exact-key", cancellation.Token);
        Assert.True(source.CanRead);
        Assert.False(stream.CanWrite);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellation.Token);
        Assert.Equal("downloaded content"u8.ToArray(), copy.ToArray());

        if (asyncDispose) await stream.DisposeAsync();
        else stream.Dispose();
        stream.Dispose();

        Assert.False(source.CanRead);
        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task DeleteUsesExactKeyAndPassesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        _client.Setup(client => client.DeleteObjectAsync(It.Is<DeleteObjectRequest>(request =>
                request.BucketName == Bucket && request.Key == "folder/exact-key"), cancellation.Token))
            .ReturnsAsync(new DeleteObjectResponse());

        await Create().DeleteAsync("folder/exact-key", cancellation.Token);

        _client.VerifyAll();
    }

    [Fact]
    public async Task MissingObjectMapsToFileNotFoundAndDeleteIsIdempotent()
    {
        var missing = new AmazonS3Exception("Missing object") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" };
        _client.Setup(client => client.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(missing);
        _client.Setup(client => client.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(missing);
        var storage = Create();

        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => storage.GetAsync("missing"));
        Assert.Same(missing, error.InnerException);
        await storage.DeleteAsync("missing");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "NoSuchBucket")]
    [InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
    public async Task StorageConfigurationAndPermissionErrorsAreNotHidden(HttpStatusCode status, string code)
    {
        var error = new AmazonS3Exception("S3 request failed") { StatusCode = status, ErrorCode = code };
        _client.Setup(client => client.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        _client.Setup(client => client.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        var storage = Create();

        Assert.Same(error, await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.GetAsync("key")));
        Assert.Same(error, await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.DeleteAsync("key")));
    }

    [Fact]
    public async Task FailedUploadAttemptsCleanupWithoutMaskingOriginalFailure()
    {
        string? key = null;
        var failure = new IOException("Upload response was lost");
        _client.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => key = request.Key)
            .ThrowsAsync(failure);
        _client.Setup(client => client.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Cleanup unavailable"));
        using var input = new MemoryStream([1, 2, 3]);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Create().SaveAsync(input, "test.txt")));

        _client.Verify(client => client.DeleteObjectAsync(It.Is<DeleteObjectRequest>(request =>
            request.BucketName == Bucket && request.Key == key), It.Is<CancellationToken>(token => !token.IsCancellationRequested)), Times.Once);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task CancelledUploadUsesIndependentCleanupTokenAndPreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        _client.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), cancellation.Token))
            .Callback(cancellation.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        _client.Setup(client => client.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.Is<CancellationToken>(token => !token.IsCancellationRequested)))
            .ReturnsAsync(new DeleteObjectResponse());
        using var input = new MemoryStream([1]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().SaveAsync(input, "test.txt", cancellation.Token));

        _client.Verify(client => client.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.Is<CancellationToken>(token => token != cancellation.Token)), Times.Once);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task AlreadyCancelledUploadDoesNotCallS3()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new MemoryStream([1]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().SaveAsync(input, "test.txt", cancellation.Token));

        _client.VerifyNoOtherCalls();
    }

    private S3FileStorageService Create() => new(_client.Object,
        Options.Create(new S3StorageOptions { BucketName = Bucket }), NullLogger<S3FileStorageService>.Instance);

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int DisposeCount { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeCount++;
            base.Dispose(disposing);
        }
    }
}
