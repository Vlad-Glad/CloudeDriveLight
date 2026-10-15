using System.IO;

namespace GDriveLight.Desktop.Services.Preview;

public enum FilePreviewKind { Unsupported, Image, Text }

public static class FilePreviewPolicy
{
    public const int MaxPreviewBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".ico"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".txt", ".md", ".markdown", ".json", ".jsonc", ".xml", ".csv", ".tsv", ".log",
        ".config", ".yaml", ".yml", ".toml", ".ini", ".js", ".jsx", ".ts", ".tsx", ".css",
        ".scss", ".html", ".htm", ".svg", ".xaml", ".razor", ".cshtml", ".sql", ".py",
        ".java", ".kt", ".c", ".h", ".cpp", ".hpp", ".go", ".rs", ".rb", ".php", ".vb", ".fs",
        ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".props", ".targets", ".resx",
        ".sh", ".ps1", ".bat", ".cmd", ".editorconfig", ".gitignore", ".gitattributes"
    };

    public static FilePreviewKind GetKind(string name)
    {
        var extension = Path.GetExtension(name);
        if (ImageExtensions.Contains(extension)) return FilePreviewKind.Image;
        if (TextExtensions.Contains(extension) ||
            Path.GetFileName(name).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(name).Equals("Makefile", StringComparison.OrdinalIgnoreCase)) return FilePreviewKind.Text;
        return FilePreviewKind.Unsupported;
    }
}

public sealed class PreviewUnavailableException(string message) : Exception(message);
