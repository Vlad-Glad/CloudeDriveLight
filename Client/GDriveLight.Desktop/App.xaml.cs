using System.Windows;
using GDriveLight.Desktop.ViewModels;
using GDriveLight.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GDriveLight.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;
    private IServiceScope? _sessionScope;
    private bool _switchingSession;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection();
        services.AddDesktopServices();
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true, ValidateScopes = true
        });
        ShowLoginFlow();
    }

    private void ShowLoginFlow()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using (var loginScope = _services!.CreateScope())
        {
            var loginWindow = loginScope.ServiceProvider.GetRequiredService<LoginWindow>();
            MainWindow = loginWindow;
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        _sessionScope = _services!.CreateScope();
        var mainWindow = _sessionScope.ServiceProvider.GetRequiredService<MainWindow>();
        var viewModel = _sessionScope.ServiceProvider.GetRequiredService<MainWindowViewModel>();
        viewModel.SessionEnded += EndSession;
        mainWindow.Closed += (_, _) => viewModel.SessionEnded -= EndSession;
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    private void EndSession(object? sender, EventArgs e)
    {
        if (_switchingSession)
        {
            return;
        }

        _switchingSession = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow?.Close();
        _sessionScope?.Dispose();
        _sessionScope = null;
        try
        {
            ShowLoginFlow();
        }
        finally
        {
            _switchingSession = false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sessionScope?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}
