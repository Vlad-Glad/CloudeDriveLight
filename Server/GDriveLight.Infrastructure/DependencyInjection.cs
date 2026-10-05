using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDriveFileRepository, DriveFileRepository>();
        services.AddScoped<IDriveFolderRepository, DriveFolderRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFileTypeRepository, FileTypeRepository>();
        services.AddScoped<ISyncFolderRepository, SyncFolderRepository>();
        services.AddScoped<ISyncFileStateRepository, SyncFileStateRepository>();

        return services;
    }
}
