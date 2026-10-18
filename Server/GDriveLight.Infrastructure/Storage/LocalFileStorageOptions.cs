namespace GDriveLight.Infrastructure.Storage;

public sealed class LocalFileStorageOptions
{
    public const string SectionName = "Storage";

    public string Path { get; set; } = "uploads";
}
