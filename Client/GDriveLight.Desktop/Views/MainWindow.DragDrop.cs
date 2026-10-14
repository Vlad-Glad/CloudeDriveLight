using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.ViewModels;

namespace GDriveLight.Desktop.Views;

public partial class MainWindow
{
    private const string InternalDragFormat = "GDriveLight.Desktop.ItemMove";
    private Point _dragStart;
    private FileItemViewModel? _dragCandidate;
    private IReadOnlyList<FileItemViewModel>? _activeDraggedItems;
    private bool _deferSelection;
    private AdornerLayer? _dropLayer;
    private DropTargetAdorner? _dropHighlight;

    private void FileGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;
        _deferSelection = false;
        var source = e.OriginalSource as DependencyObject;
        if (e.ClickCount != 1 || !_viewModel.CanManageFiles || FindAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(source) is { Item: FileItemViewModel { IsFolder: false } item } row)
        {
            _dragStart = e.GetPosition(FileGrid);
            _dragCandidate = item;
            if (row.IsSelected && FileGrid.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                // Keep the group selected until a mouse-up confirms this was just a click.
                _deferSelection = true;
                e.Handled = true;
            }
        }
    }

    private void FileGridMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_deferSelection && _dragCandidate is { } item)
        {
            FileGrid.SelectedItems.Clear();
            FileGrid.SelectedItem = item;
            e.Handled = true;
        }
        _deferSelection = false;
        _dragCandidate = null;
    }

    private async void FileGridMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !_viewModel.CanManageFiles)
        {
            _dragCandidate = null;
            return;
        }

        var position = e.GetPosition(FileGrid);
        if (_dragCandidate is null ||
            (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
             Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance))
        {
            return;
        }

        var item = _dragCandidate;
        _dragCandidate = null;
        _deferSelection = false;
        var items = FileGrid.SelectedItems.OfType<FileItemViewModel>().ToArray();
        if (!items.Contains(item) || items.Any(selected => selected.IsFolder)) return;
        if (_openMenu is not null)
        {
            _openMenu.IsOpen = false;
        }

        _activeDraggedItems = items;
        try
        {
            e.Handled = true;
            await _viewModel.DragFilesAsync(items, InternalDragFormat);
        }
        finally
        {
            ClearDropHighlight();
            _dragCandidate = null;
            _activeDraggedItems = null;
        }

        e.Handled = true;
    }

    private void ItemDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (TryGetMoveRequest(e, out var request, out var target))
        {
            e.Effects = _viewModel.IsSyncedFoldersView && request?.Destination.Id is null ? DragDropEffects.Copy : DragDropEffects.Move;
            ShowDropHighlight(target!);
        }
        else if (TryGetUploadRequest(e, out var upload, out target))
        {
            e.Effects = DragDropEffects.Copy;
            ShowDropHighlight(target!, $"Upload {upload!.Paths.Count} file(s) to {upload.Destination.Name}");
        }
        else
        {
            ClearDropHighlight();
        }

        e.Handled = true;
    }

    private void ItemDrop(object sender, DragEventArgs e)
    {
        ClearDropHighlight();
        e.Effects = DragDropEffects.None;
        if (TryGetMoveRequest(e, out var request, out _))
        {
            e.Effects = _viewModel.IsSyncedFoldersView && request?.Destination.Id is null ? DragDropEffects.Copy : DragDropEffects.Move;
            _viewModel.MoveFilesToCommand.Execute(request);
        }
        else if (TryGetUploadRequest(e, out var upload, out _))
        {
            e.Effects = DragDropEffects.Copy;
            _viewModel.UploadFilesCommand.Execute(upload);
        }

        e.Handled = true;
    }

    private void ItemDragLeave(object sender, DragEventArgs e) => ClearDropHighlight();

    private bool TryGetMoveRequest(DragEventArgs e, out FilesMoveRequest? request, out FrameworkElement? target)
    {
        request = null;
        target = null;
        if (_activeDraggedItems is null || (e.AllowedEffects & DragDropEffects.Move) == 0 ||
            !e.Data.GetDataPresent(InternalDragFormat, autoConvert: false))
        {
            return false;
        }

        var draggedId = e.Data.GetData(InternalDragFormat, autoConvert: false) switch
        {
            string id => id,
            MemoryStream stream => Encoding.Unicode.GetString(stream.ToArray()).TrimEnd('\0'),
            _ => null
        };
        if (draggedId != string.Join("\n", _activeDraggedItems.Select(item => item.Id.ToString("D")))) return false;

        if (!TryGetDropDestination(e.OriginalSource as DependencyObject, false, out var destination, out target)) return false;

        request = new FilesMoveRequest(_activeDraggedItems, destination!);
        return _viewModel.MoveFilesToCommand.CanExecute(request);
    }

    private bool TryGetUploadRequest(DragEventArgs e, out FileUploadRequest? request, out FrameworkElement? target)
    {
        request = null;
        target = null;
        if (!_viewModel.CanManageFiles || _activeDraggedItems is not null ||
            (e.AllowedEffects & DragDropEffects.Copy) == 0 ||
            e.Data.GetDataPresent(InternalDragFormat, autoConvert: false) ||
            !e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false) ||
            e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] { Length: > 0 } paths ||
            paths.Any(path => !File.Exists(path))) return false;

        if (!TryGetDropDestination(e.OriginalSource as DependencyObject, true, out var destination, out target)) return false;
        request = new FileUploadRequest(paths, destination!);
        return _viewModel.UploadFilesCommand.CanExecute(request);
    }

    private bool TryGetDropDestination(DependencyObject? source, bool allowCurrentFolder,
        out FolderDestination? destination, out FrameworkElement? target)
    {
        destination = null;
        target = null;
        while (source is not null)
        {
            if (source is DataGridRow { Item: FileItemViewModel { IsFolder: true } folder } row)
            {
                target = row;
                destination = new FolderDestination(folder.Id, folder.Name);
                return true;
            }

            if (source is Button { DataContext: BreadcrumbItem crumb } button)
            {
                target = button;
                destination = new FolderDestination(crumb.Id, crumb.Name);
                return true;
            }

            if (ReferenceEquals(source, HomeSidebarItem))
            {
                target = HomeSidebarItem;
                destination = new FolderDestination(null, "Home");
                return true;
            }

            if (allowCurrentFolder && ReferenceEquals(source, FileDropSurface) && string.IsNullOrWhiteSpace(_viewModel.SearchText))
            {
                target = FileDropSurface;
                destination = new FolderDestination(_viewModel.Breadcrumbs.Last().Id, _viewModel.CurrentFolderName);
                return true;
            }

            source = GetParent(source);
        }

        return false;
    }

    private void ShowDropHighlight(FrameworkElement target, string? uploadHint = null)
    {
        if (!ReferenceEquals(_dropHighlight?.AdornedElement, target))
        {
            ClearDropHighlight();
            _dropLayer = AdornerLayer.GetAdornerLayer(target);
            if (_dropLayer is not null)
            {
                _dropHighlight = new DropTargetAdorner(target, (Brush)FindResource("AccentBrush")) { IsHitTestVisible = false };
                _dropLayer.Add(_dropHighlight);
            }
        }

        DropUploadHint.Text = uploadHint ?? string.Empty;
        DropUploadBanner.Visibility = uploadHint is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearDropHighlight()
    {
        if (_dropHighlight is not null)
        {
            _dropLayer?.Remove(_dropHighlight);
        }

        _dropHighlight = null;
        _dropLayer = null;
        DropUploadBanner.Visibility = Visibility.Collapsed;
    }

    private sealed class DropTargetAdorner(UIElement target, Brush accent) : Adorner(target)
    {
        protected override void OnRender(DrawingContext drawingContext)
        {
            var fill = accent.CloneCurrentValue();
            fill.Opacity = 0.12;
            var bounds = new Rect(AdornedElement.RenderSize);
            bounds.Inflate(-1, -1);
            drawingContext.DrawRoundedRectangle(fill, new Pen(accent, 1.5), bounds, 6, 6);
        }
    }
}
