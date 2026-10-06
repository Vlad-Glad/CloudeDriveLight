using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("GDriveLightDb"));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDriveFileRepository, DriveFileRepository>();
        services.AddScoped<IDriveFolderRepository, DriveFolderRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFileTypeRepository, FileTypeRepository>();
        services.AddScoped<ISyncFolderRepository, SyncFolderRepository>();
        services.AddScoped<ISyncFileStateRepository, SyncFileStateRepository>();

        services.AddSingleton<GDriveLight.Application.Abstractions.Services.IFileStorageService, GDriveLight.Infrastructure.Services.LocalFileStorageService>();

        return services;
    }
}
