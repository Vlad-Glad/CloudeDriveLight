using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Storage;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Identity;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using GDriveLight.Infrastructure.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GDriveLight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentity<ApplicationUserEntity, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));

        services.AddOptions<StorageQuotaOptions>()
            .Bind(configuration.GetSection(StorageQuotaOptions.SectionName))
            .Validate(options => options.DefaultQuotaBytes >= 0, "Storage:DefaultQuotaBytes must not be negative.")
            .ValidateOnStart();

        services.AddSingleton<IStorageQuotaPolicy>(serviceProvider => new DefaultStorageQuotaPolicy(
            serviceProvider.GetRequiredService<IOptions<StorageQuotaOptions>>().Value.DefaultQuotaBytes));

        services.AddOptions<FileContentPreparationOptions>()
            .Bind(configuration.GetSection(FileContentPreparationOptions.SectionName))
            .Validate(options => options.MaxFileSizeBytes > 0 && options.MaxFileSizeBytes < long.MaxValue,
                "Storage:MaxFileSizeBytes must be positive and less than Int64.MaxValue.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TemporaryPath), "Storage:TemporaryPath must not be empty.")
            .ValidateOnStart();
        services.AddSingleton<IFileContentPreparer, FileContentPreparer>();

        services.AddScoped<IIdentityService, IdentityService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDriveFileRepository, DriveFileRepository>();
        services.AddScoped<IDriveFolderRepository, DriveFolderRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFileTypeRepository, FileTypeRepository>();
        services.AddScoped<ISyncFolderRepository, SyncFolderRepository>();
        services.AddScoped<IChangeLogRepository, ChangeLogRepository>();

        services.AddFileStorage(configuration);

        return services;
    }
}
