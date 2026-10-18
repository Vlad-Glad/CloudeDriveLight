using GDriveLight.Application;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Exceptions;
using GDriveLight.Application.Storage;
using GDriveLight.Infrastructure;
using GDriveLight.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GDriveLight.Tests.Infrastructure;

public sealed class StorageConfigurationTests : IDisposable
{
    private readonly string _testTempDir;

    public StorageConfigurationTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"gdrivelight_cfg_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempDir))
        {
            Directory.Delete(_testTempDir, true);
        }
    }

    [Theory]
    [InlineData(false, 5368709120L)]
    [InlineData(true, 5368709120L)]
    [InlineData(false, 0L)]
    [InlineData(true, 0L)]
    public void StorageQuotaPolicy_UsesConfiguredQuotaRegardlessOfRegistrationOrder(bool infrastructureFirst, long quota)
    {
        var configuration = CreateConfiguration(("Storage:DefaultQuotaBytes", quota.ToString()));
        var services = new ServiceCollection();
        if (infrastructureFirst)
        {
            services.AddInfrastructure(configuration);
            services.AddApplicationServices();
        }
        else
        {
            services.AddApplicationServices();
            services.AddInfrastructure(configuration);
        }
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IStorageQuotaPolicy>();

        var limit = policy.GetLimitBytes(Guid.NewGuid());

        Assert.Equal(quota, limit);
        if (quota == 0)
            Assert.Throws<StorageQuotaExceededException>(() => new StorageUsage(limit, 0).EnsureCanGrow(1));
    }

    [Fact]
    public async Task LocalFileStorageService_UsesConfiguredStoragePathAndSanitizesFileName()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration());
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IFileStorageService>();

        using var stream = new MemoryStream("hello content"u8.ToArray());
        var savedKey = await service.SaveAsync(stream, "../traversal_test.txt");

        Assert.True(Directory.Exists(_testTempDir));
        Assert.DoesNotContain("..", savedKey);
        Assert.True(File.Exists(Path.Combine(_testTempDir, savedKey)));

        await using (var savedStream = await service.GetAsync(savedKey))
        using (var reader = new StreamReader(savedStream))
            Assert.Equal("hello content", await reader.ReadToEndAsync());

        await service.DeleteAsync(savedKey);
        Assert.False(File.Exists(Path.Combine(_testTempDir, savedKey)));
    }

    [Theory]
    [InlineData("S3")]
    [InlineData("S33")]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInfrastructure_RejectsUnsupportedProvider(string storageProvider)
    {
        var configuration = CreateConfiguration(("Storage:Provider", storageProvider));
        var services = new ServiceCollection();

        var exception = Assert.Throws<NotSupportedException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Contains("Only 'Local' is available", exception.Message);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData(null)]
    public void AddInfrastructure_UsesLocalStorage(string? storageProvider)
    {
        var configuration = CreateConfiguration(("Storage:Provider", storageProvider));
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<LocalFileStorageService>(provider.GetRequiredService<IFileStorageService>());
    }

    [Fact]
    public void StorageQuotaPolicy_UsesDefaultWhenQuotaIsMissing()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration());
        using var provider = services.BuildServiceProvider();

        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes,
            provider.GetRequiredService<IStorageQuotaPolicy>().GetLimitBytes(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Storage:DefaultQuotaBytes", "-1")]
    [InlineData("Storage:Path", "")]
    [InlineData("Storage:Path", " ")]
    public async Task AddInfrastructure_RejectsInvalidStorageConfigurationAtStartup(string key, string value)
    {
        using var host = new HostBuilder()
            .ConfigureServices((_, services) => services.AddInfrastructure(CreateConfiguration((key, value))))
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(key, exception.Message);
    }

    private IConfiguration CreateConfiguration(params (string Key, string? Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["Storage:Path"] = _testTempDir
        };
        foreach (var (key, value) in settings)
            values[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
