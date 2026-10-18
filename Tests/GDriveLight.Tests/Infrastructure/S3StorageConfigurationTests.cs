using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure;
using GDriveLight.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GDriveLight.Tests.Infrastructure;

public sealed class S3StorageConfigurationTests
{
    [Theory]
    [InlineData("S3")]
    [InlineData("s3")]
    public void S3ProviderRegistersSingletonClientAndStorageWithoutUsingLocalPath(string providerName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration(("Storage:Provider", providerName), ("Storage:Path", "")));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<S3FileStorageService>(provider.GetRequiredService<IFileStorageService>());
        var client = provider.GetRequiredService<IAmazonS3>();
        Assert.Same(client, provider.GetRequiredService<IAmazonS3>());
        Assert.Equal("us-east-1", client.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public void CustomEndpointUsesConfiguredSigningRegionAndPathStyle()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(("Storage:S3:ServiceUrl", "http://127.0.0.1:9000"), ("Storage:S3:ForcePathStyle", "true")));
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IAmazonS3>();
        var config = Assert.IsType<AmazonS3Config>(client.Config);
        Assert.Equal("http://127.0.0.1:9000", config.ServiceURL.TrimEnd('/'));
        Assert.Equal("us-east-1", config.AuthenticationRegion);
        Assert.True(config.ForcePathStyle);
    }

    [Fact]
    public void EmptyKeysUseSdkCredentialChainAndSessionCredentialsAreSupported()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(("Storage:S3:AccessKey", ""), ("Storage:S3:SecretKey", "")));
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetRequiredService<AWSOptions>().Credentials);

        var sessionServices = new ServiceCollection();
        sessionServices.AddInfrastructure(Configuration(("Storage:S3:SessionToken", "test-session-token")));
        using var sessionProvider = sessionServices.BuildServiceProvider();
        Assert.IsType<SessionAWSCredentials>(sessionProvider.GetRequiredService<AWSOptions>().Credentials);
    }

    [Theory]
    [InlineData("Storage:S3:BucketName", "")]
    [InlineData("Storage:S3:BucketName", "<YOUR_S3_BUCKET>")]
    [InlineData("Storage:S3:Region", "")]
    [InlineData("Storage:S3:Region", "<YOUR_AWS_REGION>")]
    [InlineData("Storage:S3:ServiceUrl", "ftp://storage.example")]
    [InlineData("Storage:S3:ServiceUrl", "<YOUR_ENDPOINT>")]
    [InlineData("Storage:S3:AccessKey", "")]
    [InlineData("Storage:S3:AccessKey", "<YOUR_ACCESS_KEY>")]
    [InlineData("Storage:S3:SecretKey", "")]
    [InlineData("Storage:S3:SecretKey", "<YOUR_SECRET_KEY>")]
    [InlineData("Storage:S3:SessionToken", "<YOUR_SESSION_TOKEN>")]
    public async Task InvalidS3SettingsFailAtStartupWithoutContactingCloud(string key, string value)
    {
        using var host = new HostBuilder().ConfigureServices((_, services) => services.AddInfrastructure(Configuration((key, value)))).Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(key[(key.LastIndexOf(':') + 1)..], error.Message);
    }

    [Fact]
    public async Task LocalProviderDoesNotValidateInactiveS3Placeholders()
    {
        using var host = new HostBuilder().ConfigureServices((_, services) => services.AddInfrastructure(Configuration(
            ("Storage:Provider", "Local"), ("Storage:S3:BucketName", "<YOUR_S3_BUCKET>"),
            ("Storage:S3:Region", "<YOUR_AWS_REGION>"), ("Storage:S3:AccessKey", "<YOUR_ACCESS_KEY>"),
            ("Storage:S3:SecretKey", "<YOUR_SECRET_KEY>")))).Build();

        await host.StartAsync();
        await host.StopAsync();
        Assert.Null(host.Services.GetService<IAmazonS3>());
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["Storage:Provider"] = "S3",
            ["Storage:S3:BucketName"] = "private-test-files",
            ["Storage:S3:Region"] = "us-east-1",
            ["Storage:S3:AccessKey"] = "test-access-key",
            ["Storage:S3:SecretKey"] = "test-secret-key"
        };
        foreach (var (key, value) in settings) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
