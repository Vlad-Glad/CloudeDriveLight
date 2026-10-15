using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.ViewModels;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace GDriveLight.Desktop.Views;

public partial class LoginWindow : Window
{
    private bool _isPasswordRevealed;

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
            PasswordRevealInput.Clear();
        };
    }

    private void OnLoginSucceeded(object? sender, EventArgs e) => DialogResult = true;

    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isPasswordRevealed && DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = ((PasswordBox)sender).Password;
        }
    }

    private void PasswordRevealChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPasswordRevealed && DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordRevealInput.Text;
        }
    }

    private void TogglePasswordClick(object sender, RoutedEventArgs e)
    {
        _isPasswordRevealed = !_isPasswordRevealed;
        if (_isPasswordRevealed)
        {
            PasswordRevealInput.Text = PasswordInput.Password;
            PasswordRevealInput.Visibility = Visibility.Visible;
            PasswordInput.Visibility = Visibility.Collapsed;
            TogglePasswordIcon.Symbol = SymbolRegular.EyeOff24;
            PasswordRevealInput.Focus();
            PasswordRevealInput.CaretIndex = PasswordRevealInput.Text.Length;
        }
        else
        {
            PasswordInput.Password = PasswordRevealInput.Text;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordRevealInput.Visibility = Visibility.Collapsed;
            TogglePasswordIcon.Symbol = SymbolRegular.Eye24;
            PasswordInput.Focus();
        }
    }
}
