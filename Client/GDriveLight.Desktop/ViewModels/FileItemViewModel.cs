using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.ViewModels;

public sealed class FileItemViewModel : ViewModelBase
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DateModified { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    
    public SymbolRegular Icon { get; init; } = SymbolRegular.Document24;
    public string IconColorBrushKey { get; init; } = "TextPrimaryBrush";
    
    public bool IsFolder { get; init; }
    public Guid? FolderId { get; init; }
}
