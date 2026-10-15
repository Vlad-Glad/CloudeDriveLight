using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using GDriveLight.Api.Contracts.Files;
using GDriveLight.Api.Controllers;
using GDriveLight.Api.Services;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Application.Files.Queries;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace GDriveLight.Tests.Api;

public sealed class FileRoutesTests
{
    [Fact]
    public async Task CatalogRequiresAuthenticationAndDoesNotShadowExistingFileRoutes()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var file = new FileQuery(fileId, "photo.png", 12, "hash", 10, null, userId, DateTime.UtcNow, null, "storage/file");
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(service => service.Send(It.IsAny<GetFileTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<FileTypeQuery>>.Success(new[] { new FileTypeQuery(12, "PNG", ".png", "image/png", "image") }));
        mediator.Setup(service => service.Send(new GetFileByIdQuery(fileId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FileQuery>.Success(file));
        mediator.Setup(service => service.Send(new SearchFilesQuery("photo", userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<FileQuery>>.Success(new[] { file }));
        mediator.Setup(service => service.Send(new GetFilesByFolderQuery(null, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<FileQuery>>.Success(new[] { file }));

        var previewId = Guid.NewGuid();
        var unsupportedId = Guid.NewGuid();
        var deniedId = Guid.NewGuid();
        var unsupportedStream = new MemoryStream(new byte[] { 1, 2 });
        mediator.Setup(service => service.Send(new DownloadFileQuery(fileId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<DownloadFileResult>.Success(new(new MemoryStream(new byte[] { 1, 2, 3 }), "photo.png", "image/png")));
        mediator.Setup(service => service.Send(new DownloadFileQuery(previewId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<DownloadFileResult>.Success(new(new MemoryStream(Encoding.UTF8.GetBytes("<script>alert(1)</script>")), "renamed.png", "text/html")));
        mediator.Setup(service => service.Send(new DownloadFileQuery(unsupportedId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DownloadFileResult>.Success(new(unsupportedStream, "archive.zip", "application/zip")));
        mediator.Setup(service => service.Send(new DownloadFileQuery(deniedId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DownloadFileResult>.Failure("You do not have permission to download this file."));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("file-route-tests-only-signing-key-32-bytes"));
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly);
        builder.Services.AddSingleton(mediator.Object);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey
            };
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };

        using var anonymous = await client.GetAsync("/api/files/types");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        mediator.Verify(service => service.Send(It.IsAny<GetFileTypesQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        using var anonymousPreview = await client.GetAsync($"/api/files/{fileId}/preview");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPreview.StatusCode);
        mediator.Verify(service => service.Send(It.IsAny<DownloadFileQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        var types = await client.GetFromJsonAsync<FileTypeResponse[]>("/api/files/types");
        Assert.Equal(".png", Assert.Single(types!).Extension);
        Assert.Equal(fileId, (await client.GetFromJsonAsync<FileResponse>($"/api/files/{fileId}"))!.Id);
        Assert.Equal(fileId, Assert.Single((await client.GetFromJsonAsync<FileResponse[]>("/api/files/search?name=photo"))!).Id);
        Assert.Equal(fileId, Assert.Single((await client.GetFromJsonAsync<FileResponse[]>("/api/files"))!).Id);
        Assert.Equal("image/png", Assert.Single(types!).MimeType);
        Assert.Equal("image", Assert.Single(types!).PreviewKind);
        using var preview = await client.GetAsync($"/api/files/{fileId}/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("inline", preview.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(preview.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(preview.Headers.CacheControl!.NoStore);

        using var text = await client.GetAsync($"/api/files/{previewId}/preview");
        Assert.Equal("text/plain", text.Content.Headers.ContentType!.MediaType);
        Assert.Equal("<script>alert(1)</script>", await text.Content.ReadAsStringAsync());
        Assert.Contains("sandbox", Assert.Single(text.Headers.GetValues("Content-Security-Policy")));

        using var unsupported = await client.GetAsync($"/api/files/{unsupportedId}/preview");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        Assert.False(unsupportedStream.CanRead);
        using var denied = await client.GetAsync($"/api/files/{deniedId}/preview");
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Contains("permission", await denied.Content.ReadAsStringAsync());

        using var download = await client.GetAsync($"/api/files/{fileId}/download");
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        mediator.VerifyAll();
    }
}
