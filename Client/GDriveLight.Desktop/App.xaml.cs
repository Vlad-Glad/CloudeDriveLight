using System.Windows;
using GDriveLight.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddDesktopServices();

        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        ShowLoginFlow();
    }

    public void ShowLoginFlow()
    {
        var loginWindow = _services!.GetRequiredService<LoginWindow>();
        var result = loginWindow.ShowDialog();

        if (result == true)
        {
            MainWindow = _services!.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        else
        {
            Current.Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
