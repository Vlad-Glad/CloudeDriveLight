using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GDriveLight.Desktop.Services;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class GravatarAvatarServiceTests
{
    [Fact]
    public async Task NormalizesAndHashesEmailAndCachesAFrozenImage()
    {
        var requests = 0;
        using var service = CreateService((request, _) =>
        {
            requests++;
            Assert.Equal("https://www.gravatar.com/avatar/84059b07d4be67b806386c0aad8070a23f18836bbaae342275dc0a83414c32ee?s=128&d=404&r=g",
                request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.DoesNotContain("example.com", request.RequestUri.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreatePng()) });
        });

        var avatar = await service.GetAvatarAsync(" MyEmailAddress@example.com ");
        var cached = await service.GetAvatarAsync("myemailaddress@example.com");

        Assert.NotNull(avatar);
        Assert.True(avatar.IsFrozen);
        Assert.Same(avatar, cached);
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task MissingOrUnavailableAvatarsAreCachedAsFallback(HttpStatusCode status)
    {
        var requests = 0;
        using var service = CreateService((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        Assert.Null(await service.GetAvatarAsync("user@example.com"));
        Assert.Null(await service.GetAvatarAsync("user@example.com"));
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrOversizedImagesLeaveTheInitialVisible(bool oversized)
    {
        using var service = CreateService((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = oversized ? new ByteArrayContent(new byte[2 * 1024 * 1024 + 1]) : new StringContent("not an image")
        }));

        Assert.Null(await service.GetAvatarAsync("user@example.com"));
    }

    [Fact]
    public async Task NetworkErrorsAndTimeoutsLeaveTheInitialVisible()
    {
        using var offline = CreateService((_, _) => throw new HttpRequestException("Offline"));
        using var timeout = CreateService((_, _) => throw new TaskCanceledException("Timeout"));

        Assert.Null(await offline.GetAvatarAsync("user@example.com"));
        Assert.Null(await timeout.GetAvatarAsync("user@example.com"));
    }

    [Fact]
    public async Task CallerCancellationPropagatesAndDoesNotCacheAFailure()
    {
        var requests = 0;
        using var cancellation = new CancellationTokenSource();
        using var service = CreateService((_, token) =>
        {
            requests++;
            if (requests == 1)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreatePng()) });
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAvatarAsync("user@example.com", cancellation.Token));
        Assert.NotNull(await service.GetAvatarAsync("user@example.com"));
        Assert.Equal(2, requests);
    }

    [Fact(Timeout = 15000)]
    public async Task TheTimeoutAlsoCancelsAStalledResponseBody()
    {
        var content = new StalledContent();
        using var service = CreateService((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        Assert.Null(await service.GetAvatarAsync("user@example.com"));
        Assert.True(content.ReadToken.IsCancellationRequested);
    }

    [Fact]
    public async Task BlankEmailsDoNotMakeNetworkRequests()
    {
        using var service = CreateService((_, _) => throw new InvalidOperationException("Unexpected request"));
        Assert.Null(await service.GetAvatarAsync(" "));
    }

    private static GravatarAvatarService CreateService(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new HttpClient(new StubHandler(send)));

    private static byte[] CreatePng()
    {
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 80, 120, 200, 255 }, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private sealed class StalledContent : HttpContent
    {
        public CancellationToken ReadToken { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            ReadToken = cancellationToken;
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
