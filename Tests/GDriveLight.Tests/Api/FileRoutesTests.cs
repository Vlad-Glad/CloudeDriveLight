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
            .ReturnsAsync(Result<IEnumerable<FileTypeQuery>>.Success(new[] { new FileTypeQuery(12, "PNG", ".png") }));
        mediator.Setup(service => service.Send(new GetFileByIdQuery(fileId, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FileQuery>.Success(file));
        mediator.Setup(service => service.Send(new SearchFilesQuery("photo", userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<FileQuery>>.Success(new[] { file }));
        mediator.Setup(service => service.Send(new GetFilesByFolderQuery(null, userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<FileQuery>>.Success(new[] { file }));

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

        var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        var types = await client.GetFromJsonAsync<FileTypeResponse[]>("/api/files/types");
        Assert.Equal(".png", Assert.Single(types!).Extension);
        Assert.Equal(fileId, (await client.GetFromJsonAsync<FileResponse>($"/api/files/{fileId}"))!.Id);
        Assert.Equal(fileId, Assert.Single((await client.GetFromJsonAsync<FileResponse[]>("/api/files/search?name=photo"))!).Id);
        Assert.Equal(fileId, Assert.Single((await client.GetFromJsonAsync<FileResponse[]>("/api/files"))!).Id);
        mediator.VerifyAll();
    }
}
