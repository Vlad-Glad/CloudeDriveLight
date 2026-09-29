namespace GDriveLight.Infrastructure.Entities;

public class FileTypeEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
}
