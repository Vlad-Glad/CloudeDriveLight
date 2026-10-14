using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class DriveApiServiceTests
{
    [Fact]
    public void ProfileClaimsAreReplacedWithTheSessionAndClearedOnLogout()
    {
        using var client = new HttpClient();
        var service = new DriveApiService(client);
        var userId = Guid.NewGuid();
        service.SetAuthToken(CreateToken(new { sub = userId, firstName = "Олена", email = "olena@example.com" }));

        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal("Олена", service.CurrentUser!.FirstName);
        Assert.Equal("olena@example.com", service.CurrentUser.Email);

        var nextId = Guid.NewGuid();
        service.SetAuthToken(CreateToken(new { nameid = nextId, firstName = "Іван", email = "ivan@example.com" }));
        Assert.Equal(nextId, service.CurrentUserId);
        Assert.Equal("Іван", service.CurrentUser!.FirstName);

        service.Logout();
        Assert.Null(service.CurrentUser);
        Assert.Null(service.CurrentUserId);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("header.%%%.signature")]
    [InlineData("header.W10.signature")]
    public void InvalidTokensClearPreviouslyDisplayedProfile(string token)
    {
        using var client = new HttpClient();
        var service = new DriveApiService(client);
        service.SetAuthToken(CreateToken(new { sub = Guid.NewGuid(), firstName = "Olena", email = "user@example.com" }));
        service.SetAuthToken(token);
        Assert.Null(service.CurrentUser);
        Assert.Null(service.CurrentUserId);
    }

    [Fact]
    public void NonStringDisplayClaimsAreIgnoredWithoutLosingTheUserId()
    {
        using var client = new HttpClient();
        var service = new DriveApiService(client);
        var id = Guid.NewGuid();
        service.SetAuthToken(CreateToken(new { sub = id, firstName = 42, email = new { invalid = true } }));
        Assert.Equal(id, service.CurrentUserId);
        Assert.Equal(string.Empty, service.CurrentUser!.FirstName);
        Assert.Equal(string.Empty, service.CurrentUser.Email);
    }

    [Fact]
    public async Task DisconnectUsesTheAuthenticatedDeviceEndpointWithoutLoggingOut()
    {
        var identity = new Moq.Mock<GDriveLight.Desktop.Services.IDeviceIdentifierService>();
        var id = Guid.NewGuid();
        identity.Setup(d => d.GetDeviceId()).Returns(id);
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal($"/api/devices/{id}", request.RequestUri!.AbsolutePath);
            Assert.Equal("session-token", request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }));
        var service = new DriveApiService(client, identity.Object);
        service.SetAuthToken("session-token");
        await service.DisconnectDeviceAsync();
    }

    private static string CreateToken(object claims)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(claims);
        return "header." + Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
    }

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
    public async Task CloudRequestsDoNotIncludeDeviceOrigin()
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

        Assert.Null(capturedDeviceIdHeader);
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

    [Fact]
    public async Task CloudMutationsDoNotIncludeDeviceOrigin()
    {
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            Assert.False(request.Headers.Contains("X-Device-Id"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"folderId\":\"{Guid.NewGuid()}\"}}", Encoding.UTF8, "application/json")
            });
        }));
        var service = new DriveApiService(client);
        await service.CreateFolderAsync("New", Guid.NewGuid());
        await service.RenameAsync(Guid.NewGuid(), false, "renamed.txt");
        await service.MoveAsync(Guid.NewGuid(), false, null);
        await service.DeleteAsync(Guid.NewGuid(), false);
    }

    [Fact]
    public async Task SyncMutationsIncludeOriginAndPermitEmptyFileUploads()
    {
        var device = Guid.NewGuid();
        var identifier = new Moq.Mock<GDriveLight.Desktop.Services.IDeviceIdentifierService>();
        identifier.Setup(d => d.GetDeviceId()).Returns(device);
        var path = Path.Combine(Path.GetTempPath(), $"gdl-empty-{Guid.NewGuid():N}.txt");
        await System.IO.File.WriteAllTextAsync(path, string.Empty);
        var requests = 0;
        using var client = new HttpClient(new StubHandler(async (request, _) =>
        {
            requests++;
            Assert.Equal(device.ToString(), request.Headers.GetValues("X-Device-Id").Single());
            if (request.Content is MultipartFormDataContent multipart)
                Assert.Empty(await multipart.First().ReadAsByteArrayAsync());
            var id = Guid.NewGuid();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"folderId\":\"{id}\",\"id\":\"{id}\"}}", Encoding.UTF8, "application/json")
            };
        }));
        var service = new DriveApiService(client, identifier.Object);
        try
        {
            await service.CreateFolderAsync("Docs", Guid.NewGuid(), true);
            await service.UploadFileAsync(path, Guid.NewGuid(), true);
            await service.DeleteAsync(Guid.NewGuid(), false, true);
            Assert.Equal(3, requests);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
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
