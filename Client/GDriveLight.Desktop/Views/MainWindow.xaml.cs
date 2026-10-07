using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void NewButtonClick(object sender, RoutedEventArgs e)
    {
        NewButton.ContextMenu.PlacementTarget = NewButton;
        NewButton.ContextMenu.IsOpen = true;
    }

    private void RowActionsClick(object sender, RoutedEventArgs e)
    {
        var parent = (DependencyObject)sender;
        while (parent is not null && parent is not DataGridRow)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        if (parent is DataGridRow row && row.ContextMenu is not null)
        {
            FileGrid.SelectedItem = row.Item;
            row.ContextMenu.PlacementTarget = row;
            row.ContextMenu.IsOpen = true;
        }
    }
}
