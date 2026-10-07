using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class LoginWindow : Window
{
    public ICommand CloseCommand { get; }

    public LoginWindow(LoginViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        CloseCommand = new RelayCommand(Close);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginSucceeded += OnLoginSucceeded;
        Closed += (_, _) =>
        {
            viewModel.LoginSucceeded -= OnLoginSucceeded;
            viewModel.Dispose();
            PasswordInput.Clear();
        };
    }

    private void OnLoginSucceeded(object? sender, EventArgs e) => DialogResult = true;

    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = ((PasswordBox)sender).Password;
        }
    }

    private void TitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
