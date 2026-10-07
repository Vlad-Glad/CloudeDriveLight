using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class FileTransferTests
{
    [Fact]
    public async Task UploadSendsTheFileAndFolderAsMultipartFormData()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "notes.txt");
            await File.WriteAllTextAsync(path, "file content");
            var folderId = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            using var client = new HttpClient(new Handler(async (request, _) =>
            {
                Assert.Equal("/api/files", request.RequestUri!.AbsolutePath);
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.StartsWith("multipart/form-data", request.Content!.Headers.ContentType!.ToString());
                var body = await request.Content.ReadAsStringAsync();
                Assert.Contains("file content", body);
                Assert.Contains("notes.txt", body);
                Assert.Contains(folderId.ToString(), body);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent($"{{\"id\":\"{fileId}\"}}", Encoding.UTF8, "application/json")
                };
            }));

            Assert.Equal(fileId, await new DriveApiService(client).UploadFileAsync(path, folderId));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task FailedDownloadPreservesAnExistingFileAndRemovesTheTemporaryFile()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(path, "original");
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new InterruptedContent() })));

            await Assert.ThrowsAsync<HttpRequestException>(() => new DriveApiService(client).DownloadFileAsync(Guid.NewGuid(), path));

            Assert.Equal("original", await File.ReadAllTextAsync(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SuccessfulDownloadReplacesTheDestinationOnlyAfterReceivingTheBody()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "existing.txt");
            await File.WriteAllTextAsync(path, "original");
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("complete") })));

            await new DriveApiService(client).DownloadFileAsync(Guid.NewGuid(), path);

            Assert.Equal("complete", await File.ReadAllTextAsync(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenameAndDeleteUseTheCorrectResourceEndpoint(bool isFolder)
    {
        var id = Guid.NewGuid();
        var paths = new List<string>();
        using var client = new HttpClient(new Handler((request, _) =>
        {
            paths.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }));
        var service = new DriveApiService(client);

        await service.RenameAsync(id, isFolder, "New name");
        await service.DeleteAsync(id, isFolder);

        var resource = isFolder ? "folders" : "files";
        Assert.Equal(new[] { $"PUT /api/{resource}/{id}/rename", $"DELETE /api/{resource}/{id}" }, paths);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task MoveSendsTheResourceSpecificDestinationIncludingHome(bool isFolder, bool toHome)
    {
        var id = Guid.NewGuid();
        Guid? destinationId = toHome ? null : Guid.NewGuid();
        using var client = new HttpClient(new Handler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"/api/{(isFolder ? "folders" : "files")}/{id}/move", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var property = Assert.Single(body.RootElement.EnumerateObject());
            Assert.Equal(isFolder ? "newParentFolderId" : "newFolderId", property.Name);
            if (destinationId.HasValue)
            {
                Assert.Equal(destinationId, property.Value.GetGuid());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, property.Value.ValueKind);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await new DriveApiService(client).MoveAsync(id, isFolder, destinationId);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gdrivelight-transfer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class InterruptedContent : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"));
            throw new IOException("Connection interrupted.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
