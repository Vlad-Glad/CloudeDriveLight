using System.Windows;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class AccountSettingsWindow : Window
{
    public AccountSettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
