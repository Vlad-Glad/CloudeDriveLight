using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class DriveApiServiceTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FailedListingThrowsInsteadOfReturningAnEmptyFolder(HttpStatusCode status)
    {
        var content = new TrackedContent("{\"error\":\"Private server details\"}");
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(status) { Content = content })));
        var service = new DriveApiService(client);

        var exception = await Assert.ThrowsAsync<DriveApiException>(() => service.GetFilesAsync(null));

        Assert.Equal(status, exception.StatusCode);
        Assert.True(content.WasDisposed);
        if (status != HttpStatusCode.BadRequest)
        {
            Assert.DoesNotContain("Private server details", exception.Message);
        }
    }

    [Fact]
    public async Task ValidationMessagesAreReadFromTheServerResponse()
    {
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"Error":"Validation Failed","ValidationErrors":{"Password":["Password is too short."]}}""",
                    Encoding.UTF8, "application/json")
            })));
        var service = new DriveApiService(client);
        var exception = await Assert.ThrowsAsync<DriveApiException>(() => service.RegisterAsync("a@b.com", "short", "A"));
        Assert.Equal("Password is too short.", exception.Message);
    }

    [Fact]
    public async Task LogoutClearsTheBearerToken()
    {
        var requests = new List<(Uri? Uri, string? Token)>();
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requests.Add((request.RequestUri, request.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }));
        var service = new DriveApiService(client);
        service.SetAuthToken("old-session");
        await service.GetFilesAsync(null);
        service.Logout();
        await service.GetFilesAsync(null);

        Assert.Equal("old-session", requests[0].Token);
        Assert.Null(requests[1].Token);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task AuthenticationNeverSendsThePreviousTokenAndDisposesTheResponse()
    {
        var content = new TrackedContent("{\"token\":\"new-session\"}");
        using var client = new HttpClient(new StubHandler(async (request, _) =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/auth/login", request.RequestUri!.AbsolutePath);
            Assert.Contains("\"email\":\"a@b.com\"", await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        var service = new DriveApiService(client);
        service.SetAuthToken("old-session");

        Assert.Equal("new-session", await service.LoginAsync("a@b.com", "password"));
        Assert.True(content.WasDisposed);
    }

    [Fact]
    public async Task RequestsIncludeXDeviceIdHeader()
    {
        var expectedDeviceId = Guid.NewGuid();
        var mockDeviceService = new Moq.Mock<GDriveLight.Desktop.Services.IDeviceIdentifierService>();
        mockDeviceService.Setup(s => s.GetDeviceId()).Returns(expectedDeviceId);

        string? capturedDeviceIdHeader = null;
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            if (request.Headers.TryGetValues("X-Device-Id", out var values))
            {
                capturedDeviceIdHeader = values.FirstOrDefault();
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }));

        var service = new DriveApiService(client, mockDeviceService.Object);
        await service.GetFilesAsync(null);

        Assert.Equal(expectedDeviceId.ToString(), capturedDeviceIdHeader);
    }

    [Fact]
    public async Task GetChangesAsync_SendsCorrectQueryAndDeserializesResponse()
    {
        var responseJson = """
        {
            "changes": [
                {
                    "id": 1,
                    "resourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                    "resourceType": "File",
                    "eventType": "Created",
                    "name": "test.txt",
                    "parentFolderId": null,
                    "contentHash": "abc",
                    "sizeBytes": 100,
                    "timestamp": "2026-10-13T00:00:00Z",
                    "originDeviceId": null
                }
            ],
            "nextCursor": 1,
            "hasMore": false
        }
        """;

        Uri? requestedUri = null;
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }));

        var service = new DriveApiService(client);
        var result = await service.GetChangesAsync(cursor: 42, limit: 10);

        Assert.NotNull(result);
        Assert.Equal("/api/sync/changes", requestedUri!.AbsolutePath);
        Assert.Contains("cursor=42", requestedUri.Query);
        Assert.Contains("limit=10", requestedUri.Query);
        Assert.Single(result.Changes);
        Assert.Equal("test.txt", result.Changes[0].Name);
        Assert.Equal(1, result.NextCursor);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task UpdateFileContentAsync_SendsMultipartPutRequest()
    {
        var fileId = Guid.NewGuid();
        HttpMethod? capturedMethod = null;
        string? capturedPath = null;
        bool isMultipart = false;

        using var client = new HttpClient(new StubHandler(async (request, _) =>
        {
            capturedMethod = request.Method;
            capturedPath = request.RequestUri!.AbsolutePath;
            isMultipart = request.Content is MultipartFormDataContent;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var service = new DriveApiService(client);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello new content"));
        await service.UpdateFileContentAsync(fileId, stream, "test.txt");

        Assert.Equal(HttpMethod.Put, capturedMethod);
        Assert.Equal($"/api/files/{fileId}/content", capturedPath);
        Assert.True(isMultipart);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class TrackedContent(string body) : StringContent(body, Encoding.UTF8, "application/json")
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
