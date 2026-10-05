namespace GDriveLight.Domain.Models;

public class FileType
{
    private const int MaxNameLength = 100;
    private const int MaxMimeTypeLength = 100;
    private const int MaxExtensionLength = 20;

    public int Id { get; private set; }

    public string Name { get; private set; }

    public string MimeType { get; private set; }

    public string Extension { get; private set; }


    public FileType(
        string name,
        string mimeType,
        string extension)
    {
        ValidateName(name);
        ValidateMimeType(mimeType);
        ValidateExtension(extension);

        Name = name;
        MimeType = mimeType;
        Extension = NormalizeExtension(extension);
    }


    public void Rename(string newName)
    {
        ValidateName(newName);

        Name = newName;
    }


    public void ChangeMimeType(string mimeType)
    {
        ValidateMimeType(mimeType);

        MimeType = mimeType;
    }


    public void ChangeExtension(string extension)
    {
        ValidateExtension(extension);

        Extension = NormalizeExtension(extension);
    }


    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "File type name cannot be empty.",
                nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"File type name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }
    }


    private static void ValidateMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException(
                "MIME type cannot be empty.",
                nameof(mimeType));
        }

        if (mimeType.Length > MaxMimeTypeLength)
        {
            throw new ArgumentException(
                $"MIME type cannot exceed {MaxMimeTypeLength} characters.",
                nameof(mimeType));
        }

        if (!mimeType.Contains('/'))
        {
            throw new ArgumentException(
                "MIME type must have a valid format, for example 'application/pdf'.",
                nameof(mimeType));
        }
    }


    private static void ValidateExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException(
                "File extension cannot be empty.",
                nameof(extension));
        }

        if (extension.Length > MaxExtensionLength)
        {
            throw new ArgumentException(
                $"File extension cannot exceed {MaxExtensionLength} characters.",
                nameof(extension));
        }
    }


    private static string NormalizeExtension(string extension)
    {
        extension = extension.Trim().ToLowerInvariant();

        return extension.StartsWith('.')
            ? extension
            : $".{extension}";
    }
}