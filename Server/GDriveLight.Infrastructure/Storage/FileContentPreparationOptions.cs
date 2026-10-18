namespace GDriveLight.Infrastructure.Storage;

public sealed class FileContentPreparationOptions
{
    public const string SectionName = "Storage";

    public long MaxFileSizeBytes { get; set; } = 524288000L;
    public string TemporaryPath { get; set; } = Path.GetTempPath();
}
