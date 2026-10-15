using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GDriveLight.Desktop.ViewModels;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace GDriveLight.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private ContextMenu? _openMenu;
    private AccountSettingsWindow? _accountSettings;
    private readonly DispatcherTimer _storageRefreshTimer;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _storageRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _storageRefreshTimer.Tick += async (_, _) =>
        {
            if (IsActive && !viewModel.StorageUsage.IsLoading)
                await viewModel.StorageUsage.RefreshAsync();
        };
        Loaded += async (_, _) =>
        {
            _storageRefreshTimer.Start();
            await Task.WhenAll(viewModel.InitializeAsync(),
                viewModel.Profile?.LoadAvatarAsync() ?? Task.CompletedTask);
        };
        Activated += async (_, _) =>
        {
            if (IsLoaded && !viewModel.StorageUsage.IsLoading)
                await viewModel.StorageUsage.RefreshAsync();
        };
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
            _storageRefreshTimer.Stop();
            if (_openMenu is not null)
            {
                _openMenu.IsOpen = false;
            }

            ProfilePopup.IsOpen = false;
            _accountSettings?.Close();
            ClearDropHighlight();
            viewModel.Dispose();
        };
    }

    private void AccountSettingsClick(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        if (_accountSettings is not null) { _accountSettings.Activate(); return; }
        _accountSettings = new AccountSettingsWindow(_viewModel) { Owner = this };
        _accountSettings.Closed += (_, _) => _accountSettings = null;
        _accountSettings.Show();
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

    private async void FileTypeFilterButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("FileTypeFilterMenuStyle") };
        var allTypes = new MenuItem { Header = "All types", IsCheckable = true, IsChecked = _viewModel.SelectedFileType is null };
        allTypes.Click += (_, _) => _viewModel.SelectedFileType = null;
        menu.Items.Add(allTypes);
        var loading = new MenuItem { Header = "Loading types...", IsEnabled = false };
        menu.Items.Add(loading);
        FileTypeFilterButton.ContextMenu = menu;
        OpenMenu(FileTypeFilterButton, menu);
        e.Handled = true;

        var types = await _viewModel.LoadFileTypesAsync();
        if (!menu.IsOpen) return;
        menu.Items.Remove(loading);
        if (types is null) return;
        foreach (var type in types)
        {
            var option = new MenuItem
            {
                Header = $"{type.Name} ({type.Extension})",
                IsCheckable = true,
                IsChecked = _viewModel.SelectedFileType?.Id == type.Id
            };
            option.Click += (_, _) => _viewModel.SelectedFileType = type;
            menu.Items.Add(option);
        }
    }

    private void ColumnsButtonClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var options = new List<(DataGridColumn Column, MenuItem Item)>();
        foreach (var column in FileGrid.Columns)
        {
            if (column == NameColumn || column == ActionsColumn) continue;

            var item = new MenuItem
            {
                Header = column.Header,
                IsCheckable = true,
                IsChecked = column.Visibility == Visibility.Visible,
                StaysOpenOnClick = true
            };
            item.Click += (_, _) => column.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            menu.Items.Add(item);
            options.Add((column, item));
        }

        menu.Items.Add(new Separator());
        var showAll = new MenuItem { Header = "Show all columns" };
        showAll.Click += (_, _) =>
        {
            foreach (var (column, item) in options)
            {
                column.Visibility = Visibility.Visible;
                item.IsChecked = true;
            }
        };
        menu.Items.Add(showAll);
        ColumnsButton.ContextMenu = menu;
        OpenMenu(ColumnsButton, menu);
        menu.HorizontalOffset = ColumnsButton.ActualWidth - menu.MinWidth;
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
