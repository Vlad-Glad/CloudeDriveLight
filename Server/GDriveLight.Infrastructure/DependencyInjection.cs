using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Identity;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using GDriveLight.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IIdentityService, IdentityService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDriveFileRepository, DriveFileRepository>();
        services.AddScoped<IDriveFolderRepository, DriveFolderRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFileTypeRepository, FileTypeRepository>();
        services.AddScoped<ISyncFolderRepository, SyncFolderRepository>();

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
