using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
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
                // Process WPF bindings without starting App's real login flow.
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                stage = "loading resources";
                application.Resources = (ResourceDictionary)XamlReader.Parse("""
                    <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
                        <ResourceDictionary.MergedDictionaries>
                            <ui:ThemesDictionary Theme="Light" />
                            <ui:ControlsDictionary />
                            <ResourceDictionary Source="pack://application:,,,/GDriveLight.Desktop;component/Resources/DesignTokens.xaml" />
                        </ResourceDictionary.MergedDictionaries>
                    </ResourceDictionary>
                    """);
                var api = new Mock<IDriveApiService>(MockBehavior.Strict);
                api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
                api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid()) });
                api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[]
                    {
                        new FileResponse(Guid.NewGuid(), "notes.txt", 2, "hash", 1024L, null, Guid.NewGuid(),
                            new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), null, "internal-path")
                    });
                api.SetupGet(service => service.CurrentUser).Returns(new UserProfile(Guid.NewGuid(), "Olena", "olena@example.com"));
                using var profile = new UserProfileViewModel(api.Object, new Mock<IAvatarService>().Object);
                using var mainViewModel = new MainWindowViewModel(api.Object,
                    new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object, profile: profile);
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
                var avatar = Assert.IsType<Button>(main.FindName("AvatarButton"));
                var popup = Assert.IsType<Popup>(main.FindName("ProfilePopup"));
                var logout = Assert.IsType<Button>(main.FindName("ProfileLogoutButton"));
                main.ShowInTaskbar = false;
                main.ShowActivated = false;
                main.Opacity = 0;
                ((Border)popup.Child).Opacity = 0;
                main.Show();
                popup.GetBindingExpression(Popup.PlacementTargetProperty)!.UpdateTarget();
                popup.GetBindingExpression(FrameworkElement.DataContextProperty)!.UpdateTarget();
                logout.GetBindingExpression(Button.CommandProperty)!.UpdateTarget();
                Assert.Same(avatar, popup.PlacementTarget);
                Assert.Same(mainViewModel, popup.DataContext);
                avatar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                logout.GetBindingExpression(Button.CommandProperty)!.UpdateTarget();
                Assert.True(popup.IsOpen);
                Assert.Same(mainViewModel.LogoutCommand, logout.Command);
                var card = (Border)popup.Child;
                logout.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent
                });
                Assert.True(popup.IsOpen);
                var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(card)!, Environment.TickCount, Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                card.RaiseEvent(escape);
                Assert.True(escape.Handled);
                Assert.False(popup.IsOpen);
                avatar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(popup.IsOpen);
                avatar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(popup.IsOpen);
                avatar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                main.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent
                });
                Assert.False(popup.IsOpen);
                Assert.Equal(6, grid.Columns.Count);
                Assert.Equal(2, mainViewModel.VisibleFiles.Count);
                var selector = Assert.IsType<ComboBox>(main.FindName("DeviceSelector"));
                Assert.Equal("SelectedDevice", selector.GetBindingExpression(ComboBox.SelectedItemProperty)!.ParentBinding.Path.Path);
                Assert.Equal("VisibleFiles", grid.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path.Path);
                Assert.Equal("Name", grid.Columns[0].Header);
                Assert.Equal("Type", grid.Columns[1].Header);
                Assert.Equal("FileType", Assert.IsType<Binding>(Assert.IsType<DataGridTextColumn>(grid.Columns[1]).Binding).Path.Path);
                Assert.Equal("Created", grid.Columns[2].Header);
                Assert.Equal("Modified", grid.Columns[3].Header);
                Assert.Equal("Size", grid.Columns[4].Header);
                var columnsButton = Assert.IsType<Button>(main.FindName("ColumnsButton"));
                columnsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var columnsMenu = Assert.IsType<ContextMenu>(columnsButton.ContextMenu);
                columnsMenu.Opacity = 0;
                Assert.Equal(new[] { "Type", "Created", "Modified", "Size", "Show all columns" },
                    columnsMenu.Items.OfType<MenuItem>().Select(item => item.Header));
                for (var index = 1; index < grid.Columns.Count - 1; index++)
                {
                    var option = Assert.IsType<MenuItem>(columnsMenu.Items[index - 1]);
                    Assert.True(option.IsChecked);
                    Assert.True(option.IsEnabled);
                    option.IsChecked = false;
                    option.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Equal(Visibility.Collapsed, grid.Columns[index].Visibility);
                }
                Assert.Equal(Visibility.Visible, grid.Columns[0].Visibility);
                Assert.Equal(Visibility.Visible, grid.Columns[5].Visibility);
                mainViewModel.RefreshAsync().GetAwaiter().GetResult();
                Assert.All(grid.Columns.Skip(1).Take(4), column => Assert.Equal(Visibility.Collapsed, column.Visibility));
                Assert.Equal(Visibility.Visible, grid.Columns[0].Visibility);
                Assert.Equal(Visibility.Visible, grid.Columns[5].Visibility);
                var showAll = Assert.IsType<MenuItem>(columnsMenu.Items[columnsMenu.Items.Count - 1]);
                showAll.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.All(grid.Columns, column => Assert.Equal(Visibility.Visible, column.Visibility));
                columnsMenu.IsOpen = false;
                var folderItem = mainViewModel.VisibleFiles.Single(item => item.IsFolder);
                Assert.Equal("Folder", folderItem.FileType);
                Assert.Equal("—", folderItem.DateCreated);
                Assert.Equal("—", folderItem.DateModified);
                Assert.Empty(folderItem.TimeCreated);
                Assert.Empty(folderItem.TimeModified);
                Assert.IsAssignableFrom<Brush>(main.FindResource("SurfaceBackgroundBrush"));
                Assert.Contains(grid.InputBindings.OfType<KeyBinding>(), binding => binding.Key == Key.F2);
                Assert.Contains(grid.InputBindings.OfType<KeyBinding>(), binding => binding.Key == Key.Delete);
                Assert.Contains(grid.InputBindings.OfType<KeyBinding>(), binding => binding.Key == Key.M && binding.Modifiers == ModifierKeys.Control);
                using var pickerViewModel = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
                var picker = new FolderPickerWindow(pickerViewModel);
                Assert.IsType<ListBox>(picker.FindName("FolderList"));
                var moveButton = (Button)picker.FindName("MoveHereButton");
                Assert.Equal("ConfirmCommand", moveButton.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path.Path);
                var nameDialog = new NameDialogWindow("Rename", "New name", "notes.txt");
                Assert.Equal("notes.txt", ((TextBox)nameDialog.FindName("NameInput")).Text);
                var accountSettings = new AccountSettingsWindow(mainViewModel);
                var disconnectButton = Assert.IsType<Button>(accountSettings.FindName("DisconnectDeviceButton"));
                accountSettings.GetBindingExpression(FrameworkElement.DataContextProperty)?.UpdateTarget();
                disconnectButton.GetBindingExpression(Button.CommandProperty)!.UpdateTarget();
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Same(mainViewModel.DisconnectDeviceCommand, disconnectButton.Command);
                accountSettings.Close();
                nameDialog.Close();
                picker.Close();
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
