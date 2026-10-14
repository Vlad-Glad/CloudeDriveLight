using System.IO;
using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.ViewModels;

public sealed class FileItemViewModel : ViewModelBase
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FileType
    {
        get
        {
            if (IsFolder) return "Folder";
            var extension = Path.GetExtension(Name);
            return extension.Length > 1 ? extension[1..].ToUpperInvariant() : "File";
        }
    }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }
    public string DateCreated => CreatedAtUtc?.ToLocalTime().ToString("d") ?? "—";
    public string TimeCreated => CreatedAtUtc?.ToLocalTime().ToString("HH:mm") ?? string.Empty;
    public string CreatedTimestamp => CreatedAtUtc?.ToLocalTime().ToString("F") ?? "—";
    public string DateModified => (ModifiedAtUtc ?? CreatedAtUtc)?.ToLocalTime().ToString("d") ?? "—";
    public string TimeModified => (ModifiedAtUtc ?? CreatedAtUtc)?.ToLocalTime().ToString("HH:mm") ?? string.Empty;
    public string ModifiedTimestamp => (ModifiedAtUtc ?? CreatedAtUtc)?.ToLocalTime().ToString("F") ?? "—";
    public string Size { get; init; } = string.Empty;
    
    public SymbolRegular Icon { get; init; } = SymbolRegular.Document24;
    public string IconColorBrushKey { get; init; } = "TextPrimaryBrush";
    
    public bool IsFolder { get; init; }
    public Guid? FolderId { get; init; }
    public string? RelativePath { get; init; }
}
