using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.ViewModels;
using GDriveLight.Desktop.Views;
using Moq;

namespace GDriveLight.Desktop.Tests.Views;

public sealed class WindowSmokeTests
{
    [Fact]
    public void WindowsLoadResourcesAndExposeFileColumnsAndMaskedPassword()
    {
        Exception? failure = null;
        var stage = "starting";
        var thread = new Thread(() =>
        {
            try
            {
                var application = new App();
                stage = "loading resources";
                application.InitializeComponent();
                var api = new Mock<IDriveApiService>(MockBehavior.Strict);
                api.Setup(service => service.GetServerUrl()).Returns("https://localhost:7220");
                api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid()) });
                api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[]
                    {
                        new FileResponse(Guid.NewGuid(), "notes.txt", 2, "hash", null, Guid.NewGuid(),
                            new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), null, "internal-path")
                    });
                using var mainViewModel = new MainWindowViewModel(api.Object,
                    new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);
                stage = "loading folder data";
                mainViewModel.InitializeAsync().GetAwaiter().GetResult();
                stage = "creating main window";
                var main = new MainWindow(mainViewModel);
                stage = "processing bindings";
                Assert.True(main.Dispatcher.CheckAccess());
                main.GetBindingExpression(Window.TitleProperty)?.UpdateTarget();
                var grid = (DataGrid)main.FindName("FileGrid");
                grid.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
                grid.GetBindingExpression(UIElement.VisibilityProperty)?.UpdateTarget();
                Assert.Equal(4, grid.Columns.Count);
                Assert.Equal("VisibleFiles", grid.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path);
                Assert.Equal("Name", grid.Columns[0].Header);
                Assert.Equal("Modified", grid.Columns[1].Header);
                Assert.Equal("Size", grid.Columns[2].Header);
                Assert.IsAssignableFrom<Brush>(main.FindResource("SurfaceBackgroundBrush"));
                stage = "creating login window";

                using var loginViewModel = new LoginViewModel(api.Object) { IsRegisterMode = true };
                var login = new LoginWindow(loginViewModel);
                Assert.IsType<PasswordBox>(login.FindName("PasswordInput"));
                login.Close();
                main.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), $"WPF resource loading timed out while {stage}.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

}
