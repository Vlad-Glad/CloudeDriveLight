using System.Windows;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Closed += (s, e) => Application.Current.Shutdown();
    }
}
