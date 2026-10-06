using System.Windows;
using System.Windows.Input;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class LoginWindow : Window
{
    public ICommand CloseCommand { get; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        CloseCommand = new RelayCommand(Close);

        viewModel.OnLoginSuccess = () =>
        {
            DialogResult = true;
            Close();
        };

        // Enable dragging
        MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }
}
