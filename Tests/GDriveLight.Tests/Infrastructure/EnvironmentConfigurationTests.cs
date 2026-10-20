using Amazon.Extensions.NETCore.Setup;
using GDriveLight.Infrastructure;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GDriveLight.Tests.Infrastructure;

public sealed class EnvironmentConfigurationTests
{
    [Fact]
    public async Task DevelopmentUsesNamedProfileAndConfiguredBucketWithoutStaticKeys()
    {
        var configuration = Configuration("Development");
        using var host = new HostBuilder().ConfigureServices((_, services) => services.AddInfrastructure(configuration)).Build();

        await host.StartAsync();
        var options = host.Services.GetRequiredService<AWSOptions>();
        var storage = host.Services.GetRequiredService<IOptions<S3StorageOptions>>().Value;

        Assert.Equal("S3", configuration["Storage:Provider"]);
        Assert.False(string.IsNullOrWhiteSpace(options.Profile));
        Assert.Equal(configuration["AWS:Region"], options.Region.SystemName);
        Assert.DoesNotContain('<', storage.BucketName);
        Assert.False(string.IsNullOrWhiteSpace(storage.BucketName));
        Assert.Null(options.Credentials);
        await host.StopAsync();
    }

    [Fact]
    public async Task ProductionLeavesCredentialResolutionToSdkWithoutInheritingDevelopmentProfile()
    {
        var configuration = Configuration("Production", ("Storage:S3:BucketName", "production-test-files"));
        using var host = new HostBuilder().ConfigureServices((_, services) => services.AddInfrastructure(configuration)).Build();

        await host.StartAsync();
        var options = host.Services.GetRequiredService<AWSOptions>();

        Assert.Equal("S3", configuration["Storage:Provider"]);
        Assert.Null(configuration["AWS:Profile"]);
        Assert.Null(options.Profile);
        Assert.Null(options.Credentials);
        Assert.Equal(configuration["AWS:Region"], options.Region.SystemName);
        Assert.Equal("production-test-files", host.Services.GetRequiredService<IOptions<S3StorageOptions>>().Value.BucketName);
        await host.StopAsync();
    }

    [Fact]
    public async Task ProductionRejectsPlaceholderBucket()
    {
        var configuration = Configuration("Production", ("Storage:S3:BucketName", "<YOUR_S3_BUCKET>"));
        using var host = new HostBuilder().ConfigureServices((_, services) => services.AddInfrastructure(configuration)).Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("BucketName", error.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void EnvironmentsRequireConnectionStringFromExternalConfiguration(string environment)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment}.json").Build();

        Assert.True(string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));
    }

    private static IConfiguration Configuration(string environment, params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused" };
        foreach (var (key, value) in settings) values[key] = value;
        return new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment}.json")
            .AddInMemoryCollection(values).Build();
    }
}
