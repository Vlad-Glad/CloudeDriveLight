using System.Windows;
using System.Windows.Input;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class FilePreviewWindow : Window
{
    private readonly FilePreviewViewModel _viewModel;

    public FilePreviewWindow(FilePreviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers != ModifierKeys.Alt || key is not (Key.Left or Key.Right)) return;
        if (key == Key.Left) _viewModel.PreviousCommand.Execute(null);
        else _viewModel.NextCommand.Execute(null);
        e.Handled = true;
    }
}
