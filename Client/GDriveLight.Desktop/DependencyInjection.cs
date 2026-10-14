using System.Net.Http;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Shell;
using GDriveLight.Desktop.ViewModels;
using GDriveLight.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Desktop;

public static class DependencyInjection
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // HTTPS uses the normal platform trust store, including trusted development certificates.
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IDeviceIdentifierService, DeviceIdentifierService>();
        services.AddSingleton<IDriveApiService, DriveApiService>();
        services.AddSingleton<IAvatarService>(_ => new GravatarAvatarService(
            new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(5) }));
        services.AddScoped<UserProfileViewModel>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IVirtualFileDragService, WindowsVirtualFileDragService>();
        services.AddSingleton<IUserDialogService, UserDialogService>();
        services.AddSingleton<Data.ILocalSyncDbContextFactory, Data.LocalSyncDbContextFactory>();
        services.AddSingleton<Services.Sync.ISyncSettingsService, Services.Sync.SyncSettingsService>();
        services.AddSingleton<Services.Sync.ISyncEngine, Services.Sync.SyncEngine>();
        services.AddSingleton<Services.Sync.IDeviceSyncConnection, Services.Sync.DeviceSyncConnection>();
        services.AddScoped<LoginViewModel>();
        services.AddScoped<LoginWindow>();
        services.AddScoped<MainWindowViewModel>();
        services.AddScoped<MainWindow>();
        return services;
    }
}
