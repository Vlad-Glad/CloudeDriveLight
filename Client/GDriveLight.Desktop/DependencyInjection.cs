using System.Net.Http;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.ViewModels;
using GDriveLight.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Desktop;

public static class DependencyInjection
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // One HTTP client per application; endpoint services will share its connection pool.
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
