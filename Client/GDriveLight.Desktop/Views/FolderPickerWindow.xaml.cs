using System.Windows;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class FolderPickerWindow : Window
{
    public FolderPickerWindow(FolderPickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closed += (_, _) =>
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.Dispose();
        };
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = ((FolderPickerViewModel)DataContext).Destination is not null;
    }
}
