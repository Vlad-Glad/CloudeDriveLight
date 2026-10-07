using System.Net.Http;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
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
        services.AddSingleton<IDriveApiService, DriveApiService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddScoped<LoginViewModel>();
        services.AddScoped<LoginWindow>();
        services.AddScoped<MainWindowViewModel>();
        services.AddScoped<MainWindow>();
        return services;
    }
}
