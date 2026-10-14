using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GDriveLight.Desktop.ViewModels;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace GDriveLight.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private ContextMenu? _openMenu;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await Task.WhenAll(viewModel.InitializeAsync(),
            viewModel.Profile?.LoadAvatarAsync() ?? Task.CompletedTask);
        Deactivated += (_, _) => ProfilePopup.IsOpen = false;
        PreviewMouseDown += (_, e) =>
        {
            if (!IsWithin(e.OriginalSource as DependencyObject, AvatarButton) &&
                !IsWithin(e.OriginalSource as DependencyObject, ProfileCard))
            {
                ProfilePopup.IsOpen = false;
            }
        };
        Closed += (_, _) =>
        {
            if (_openMenu is not null)
            {
                _openMenu.IsOpen = false;
            }

            ProfilePopup.IsOpen = false;
            ClearDropHighlight();
            viewModel.Dispose();
        };
    }

    private void ProfileButtonClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
        e.Handled = true;
    }

    private void ProfilePopupOpened(object sender, EventArgs e) => ProfileCard.Focus();

    private void ProfileCardKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        ProfilePopup.IsOpen = false;
        AvatarButton.Focus();
        e.Handled = true;
    }

    private void NewButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateAction("New folder", _viewModel.CreateFolderCommand, SymbolRegular.FolderAdd24));
        menu.Items.Add(CreateAction("Upload files", _viewModel.UploadCommand, SymbolRegular.ArrowUpload24));
        OpenMenu(NewButton, menu);
        e.Handled = true;
    }

    private void RowActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FileItemViewModel item } button)
        {
            OpenFileActions(item, button);
        }

        e.Handled = true;
    }

    private void FileGridRightClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is { Item: FileItemViewModel item } row)
        {
            OpenFileActions(item, row, atPointer: true);
            e.Handled = true;
        }
    }

    private void FileGridKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) &&
            FileGrid.SelectedItem is FileItemViewModel item)
        {
            var target = FileGrid.ItemContainerGenerator.ContainerFromItem(item) as UIElement ?? FileGrid;
            OpenFileActions(item, target);
            e.Handled = true;
        }
    }

    private void FileGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is { Item: FileItemViewModel item } &&
            _viewModel.OpenFolderCommand.CanExecute(item))
        {
            _viewModel.OpenFolderCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void OpenFileActions(FileItemViewModel item, UIElement target, bool atPointer = false)
    {
        FileGrid.SelectedItem = item;
        var menu = new ContextMenu();
        menu.Items.Add(item.IsFolder
            ? CreateAction("Open folder", _viewModel.OpenFolderCommand, SymbolRegular.FolderOpen24, item)
            : CreateAction("Download", _viewModel.DownloadCommand, SymbolRegular.ArrowDownload24, item));
        menu.Items.Add(new Separator());
        var move = CreateAction(_viewModel.IsSyncedFoldersView ? "Copy to Home..." : "Move to...", _viewModel.MoveCommand, SymbolRegular.FolderArrowRight24, item);
        move.InputGestureText = "Ctrl+M";
        menu.Items.Add(move);
        var rename = CreateAction("Rename", _viewModel.RenameCommand, SymbolRegular.Edit24, item);
        rename.InputGestureText = "F2";
        menu.Items.Add(rename);
        menu.Items.Add(new Separator());
        var delete = CreateAction("Delete", _viewModel.DeleteCommand, SymbolRegular.Delete24, item);
        delete.InputGestureText = "Delete";
        delete.Foreground = (Brush)FindResource("PdfIconBrush");
        menu.Items.Add(delete);
        OpenMenu(target, menu, atPointer);
    }

    private static MenuItem CreateAction(string label, ICommand command, SymbolRegular icon, object? item = null) => new()
    {
        Header = label,
        Command = command,
        CommandParameter = item,
        Icon = new SymbolIcon { Symbol = icon, FontSize = 18 }
    };

    private void OpenMenu(UIElement target, ContextMenu menu, bool atPointer = false)
    {
        if (_openMenu is not null)
        {
            _openMenu.IsOpen = false;
        }

        menu.PlacementTarget = target;
        menu.Placement = atPointer ? PlacementMode.MousePoint : PlacementMode.Bottom;
        menu.FontFamily = FontFamily;
        menu.FontSize = FontSize;
        menu.MinWidth = 220;
        _openMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openMenu, menu))
            {
                _openMenu = null;
            }
        };
        menu.IsOpen = true;
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, ancestor)) return true;
            element = GetParent(element);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = GetParent(element);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element) => element is Visual
        ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
