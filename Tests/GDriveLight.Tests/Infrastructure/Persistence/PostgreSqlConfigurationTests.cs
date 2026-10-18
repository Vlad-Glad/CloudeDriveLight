using GDriveLight.Infrastructure;
using GDriveLight.Api.Persistence;
using GDriveLight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Tests.Infrastructure.Persistence;

public class PostgreSqlConfigurationTests
{
    [Fact]
    public void DesignTimeFactory_UsesCommandLineConnectionOverride()
    {
        const string connectionString = "Host=design-time;Database=unused;Username=test";
        using var context = new AppDbContextFactory().CreateDbContext(
            ["--environment", "Production", "--ConnectionStrings:DefaultConnection", connectionString]);

        Assert.Equal(connectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void DesignTimeFactory_UsesDevelopmentSettingsFromApiOutput()
    {
        using var context = new AppDbContextFactory().CreateDbContext(["--environment", "Development"]);
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .AddEnvironmentVariables()
            .Build();

        Assert.Equal(configuration.GetConnectionString("DefaultConnection"), context.Database.GetConnectionString());
    }

    [Fact]
    public void DesignTimeFactory_RejectsMissingConnectionInsteadOfFallingBackToLocalDatabase()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new AppDbContextFactory().CreateDbContext(
            ["--environment", "Production", "--ConnectionStrings:DefaultConnection", ""]));

        Assert.Contains("DefaultConnection", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInfrastructure_WhenConnectionStringIsMissing_RejectsConfiguration(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("DefaultConnection", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenConfigured_UsesPostgreSql()
    {
        const string connectionString = "Host=localhost;Database=unused;Username=test;Password=test";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(connectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void Migrations_MatchTheCurrentPostgreSqlModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=test;Password=test")
            .Options;
        using var context = new AppDbContext(options);

        Assert.Contains(context.Database.GetMigrations(), migration => migration.EndsWith("_InitialCreate"));
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
