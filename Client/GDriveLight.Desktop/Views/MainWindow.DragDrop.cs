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
    private FileItemViewModel? _activeDraggedItem;
    private AdornerLayer? _dropLayer;
    private DropTargetAdorner? _dropHighlight;

    private void FileGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;
        var source = e.OriginalSource as DependencyObject;
        if (e.ClickCount != 1 || !_viewModel.CanManageFiles || FindAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(source)?.Item is FileItemViewModel item)
        {
            _dragStart = e.GetPosition(FileGrid);
            _dragCandidate = item;
        }
    }

    private void FileGridMouseMove(object sender, MouseEventArgs e)
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
        FileGrid.SelectedItem = item;
        if (_openMenu is not null)
        {
            _openMenu.IsOpen = false;
        }

        _activeDraggedItem = item;
        var data = new DataObject(InternalDragFormat, item.Id.ToString("D"), autoConvert: false);
        try
        {
            DragDrop.DoDragDrop(FileGrid, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            ClearDropHighlight();
            _dragCandidate = null;
            _activeDraggedItem = null;
        }

        e.Handled = true;
    }

    private void ItemDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (TryGetMoveRequest(e, out var request, out var target))
        {
            e.Effects = _viewModel.IsSyncedFoldersView && request?.Destination.Id is null ? DragDropEffects.Copy : DragDropEffects.Move;
            if (!ReferenceEquals(_dropHighlight?.AdornedElement, target))
            {
                ClearDropHighlight();
                _dropLayer = AdornerLayer.GetAdornerLayer(target!);
                if (_dropLayer is not null)
                {
                    _dropHighlight = new DropTargetAdorner(target!, (Brush)FindResource("AccentBrush")) { IsHitTestVisible = false };
                    _dropLayer.Add(_dropHighlight);
                }
            }
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
            _viewModel.MoveToCommand.Execute(request);
        }

        e.Handled = true;
    }

    private void ItemDragLeave(object sender, DragEventArgs e) => ClearDropHighlight();

    private bool TryGetMoveRequest(DragEventArgs e, out ItemMoveRequest? request, out FrameworkElement? target)
    {
        request = null;
        target = null;
        if (_activeDraggedItem is null || (e.AllowedEffects & DragDropEffects.Move) == 0 ||
            !e.Data.GetDataPresent(InternalDragFormat, autoConvert: false) ||
            e.Data.GetData(InternalDragFormat, autoConvert: false) is not string draggedId ||
            draggedId != _activeDraggedItem.Id.ToString("D"))
        {
            return false;
        }

        var source = e.OriginalSource as DependencyObject;
        FolderDestination? destination = null;
        while (source is not null)
        {
            if (source is DataGridRow { Item: FileItemViewModel { IsFolder: true } folder } row)
            {
                target = row;
                destination = new FolderDestination(folder.Id, folder.Name);
                break;
            }

            if (source is Button { DataContext: BreadcrumbItem crumb } button)
            {
                target = button;
                destination = new FolderDestination(crumb.Id, crumb.Name);
                break;
            }

            if (ReferenceEquals(source, HomeSidebarItem))
            {
                target = HomeSidebarItem;
                destination = new FolderDestination(null, "Home");
                break;
            }

            source = GetParent(source);
        }

        if (destination is null)
        {
            return false;
        }

        request = new ItemMoveRequest(_activeDraggedItem, destination);
        return _viewModel.MoveToCommand.CanExecute(request);
    }

    private void ClearDropHighlight()
    {
        if (_dropHighlight is not null)
        {
            _dropLayer?.Remove(_dropHighlight);
        }

        _dropHighlight = null;
        _dropLayer = null;
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
