using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GDriveLight.Api.Configuration;
using GDriveLight.Api.Controllers;
using GDriveLight.Api.Middleware;
using GDriveLight.Api.Services;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace GDriveLight.Tests.Api;

public sealed class HttpConfigurationTests
{
    private const string AllowedOrigin = "https://client.example";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("http-configuration-test-signing-key-32-bytes"));

    [Theory]
    [InlineData(false, 1024)]
    [InlineData(false, 1025)]
    [InlineData(true, 1024)]
    [InlineData(true, 1025)]
    public async Task UploadAndUpdateRespectFileLimitIndependentlyOfLargerRequestLimit(bool update, int size)
    {
        await using var app = await StartServer();
        using var client = CreateClient(app);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(new byte[size]), "file", "bounded.txt");
        // The complete request is larger than the allowed file, including at the exact file boundary.
        Assert.True((await multipart.ReadAsByteArrayAsync()).Length > 1024);
        using var request = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post,
            update ? $"/api/files/{Guid.NewGuid()}/content" : "/api/files") { Content = multipart };

        using var response = await client.SendAsync(request);

        Assert.Equal(size == 1024 ? (update ? HttpStatusCode.NoContent : HttpStatusCode.Created) : HttpStatusCode.RequestEntityTooLarge,
            response.StatusCode);
        if (size > 1024)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("file_size_limit_exceeded", body.GetProperty("code").GetString());
            Assert.Equal(1024, body.GetProperty("maxFileSizeBytes").GetInt64());
        }
    }

    [Fact]
    public async Task UploadRejectsRequestsAboveTransportLimitBeforeDispatchingCommand()
    {
        await using var app = await StartServer();
        using var client = CreateClient(app);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(new byte[2 * 1024 * 1024 + 1]), "file", "large.txt");

        using var response = await client.PostAsync("/api/files", multipart);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
        app.Services.GetRequiredService<Mock<IMediator>>().Verify(
            mediator => mediator.Send(It.IsAny<UploadFileCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AllowedOrigin, true)]
    [InlineData("https://untrusted.example", false)]
    public async Task CorsPreflightOnlyAllowsConfiguredOrigin(string origin, bool allowed)
    {
        await using var app = await StartServer();
        using var client = CreateClient(app);
        using var request = Preflight(origin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            Assert.Contains("PUT", response.Headers.GetValues("Access-Control-Allow-Methods"));
        }
    }

    [Fact]
    public async Task MissingCorsConfigurationDoesNotImplicitlyAllowLocalhost()
    {
        await using var app = await StartServer(allowedOrigin: null);
        using var client = CreateClient(app);
        using var request = Preflight("http://localhost:5173");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task MissingTransportLimitStillAllowsFileAtExactLimit()
    {
        await using var app = await StartServer(requestLimit: null);
        using var client = CreateClient(app);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(new byte[1024]), "file", "bounded.txt");

        using var response = await client.PostAsync("/api/files", multipart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("Storage:MaxFileSizeBytes", "0")]
    [InlineData("Storage:MaxFileSizeBytes", "-1")]
    [InlineData("Storage:MaxFileSizeBytes", "9223372036854775807")]
    [InlineData("Kestrel:Limits:MaxRequestBodySize", "1024")]
    public void InvalidUploadConfigurationFailsBeforeStartingServer(string key, string value)
    {
        var builder = CreateBuilder();
        builder.Configuration[key] = value;

        var error = Assert.Throws<InvalidOperationException>(() => builder.ConfigureHttp());

        Assert.Contains(key, error.Message);
    }

    private static WebApplicationBuilder CreateBuilder(string? allowedOrigin = AllowedOrigin, long? requestLimit = 2 * 1024 * 1024)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["Storage:MaxFileSizeBytes"] = "1024",
            ["Kestrel:Limits:MaxRequestBodySize"] = requestLimit?.ToString(),
            ["Cors:AllowedOrigins:0"] = allowedOrigin
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return builder;
    }

    private static async Task<WebApplication> StartServer(string? allowedOrigin = AllowedOrigin, long? requestLimit = 2 * 1024 * 1024)
    {
        var builder = CreateBuilder(allowedOrigin, requestLimit);
        builder.ConfigureHttp();
        builder.Services.AddInfrastructure(builder.Configuration);
        IServiceProvider services = null!;
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(value => value.Send(It.IsAny<UploadFileCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (UploadFileCommand command, CancellationToken token) =>
            {
                await using var content = await services.GetRequiredService<IFileContentPreparer>().PrepareAsync(command.FileStream, token);
                return Result<Guid>.Success(Guid.NewGuid());
            });
        mediator.Setup(value => value.Send(It.IsAny<UpdateFileContentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async (UpdateFileContentCommand command, CancellationToken token) =>
            {
                await using var content = await services.GetRequiredService<IFileContentPreparer>().PrepareAsync(command.FileStream, token);
                return Result.Success();
            });
        builder.Services.AddSingleton(mediator);
        builder.Services.AddSingleton(mediator.Object);
        builder.Services.AddControllers().AddApplicationPart(typeof(FilesController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = SigningKey,
                ValidateIssuerSigningKey = true
            });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        services = app.Services;
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var token = new JwtSecurityToken(claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        client.DefaultRequestHeaders.ExpectContinue = true;
        return client;
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, $"/api/files/{Guid.NewGuid()}/content");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
