using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GDriveLight.Tests.Infrastructure;

public sealed class S3StorageHttpTests : IDisposable
{
    private readonly string _credentialsPath = Path.Combine(Path.GetTempPath(), $"gdrivelight-aws-{Guid.NewGuid():N}.ini");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealSdkSavesDownloadsAndDeletesThroughConfiguredEndpoint(bool useProfile)
    {
        var objects = new ConcurrentDictionary<string, byte[]>();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.Run(async context =>
        {
            Assert.StartsWith("AWS4-HMAC-SHA256 ", context.Request.Headers.Authorization.ToString());
            Assert.Contains($"Credential={(useProfile ? "profile-access-key" : "test-access-key")}/", context.Request.Headers.Authorization.ToString());
            var path = context.Request.Path.Value!;
            Assert.StartsWith("/private-test-files/", path);
            if (HttpMethods.IsPut(context.Request.Method))
            {
                using var copy = new MemoryStream();
                await context.Request.Body.CopyToAsync(copy);
                var bytes = copy.ToArray();
                objects[path] = bytes;
                context.Response.Headers.ETag = $"\"{Convert.ToHexStringLower(MD5.HashData(bytes))}\"";
            }
            else if (HttpMethods.IsDelete(context.Request.Method))
            {
                objects.TryRemove(path, out _);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
            else if (objects.TryGetValue(path, out var bytes))
            {
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsync("<Error><Code>NoSuchKey</Code><Message>Not found</Message></Error>");
            }
        });
        await app.StartAsync();
        if (useProfile)
            await File.WriteAllTextAsync(_credentialsPath, "[test-profile]\naws_access_key_id = profile-access-key\naws_secret_access_key = profile-secret-key\n");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:BucketName"] = "private-test-files",
            ["AWS:Region"] = "us-east-1",
            ["AWS:Profile"] = useProfile ? "test-profile" : null,
            ["AWS:ProfilesLocation"] = useProfile ? _credentialsPath : null,
            ["Storage:S3:ServiceUrl"] = app.Urls.Single(),
            ["Storage:S3:ForcePathStyle"] = "true",
            ["Storage:S3:AccessKey"] = useProfile ? "" : "test-access-key",
            ["Storage:S3:SecretKey"] = useProfile ? "" : "test-secret-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorageService>();
        var content = "actual SDK round trip"u8.ToArray();
        using var input = new MemoryStream(content);

        var key = await storage.SaveAsync(input, "test.txt");
        Assert.Equal(content, objects[$"/private-test-files/{key}"]);
        Assert.True(input.CanRead);
        await using (var stream = await storage.GetAsync(key))
        {
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }
        await storage.DeleteAsync(key);
        await storage.DeleteAsync(key);

        Assert.Empty(objects);
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.GetAsync(key));
        await app.StopAsync();
    }

    public void Dispose()
    {
        if (File.Exists(_credentialsPath)) File.Delete(_credentialsPath);
    }
}
