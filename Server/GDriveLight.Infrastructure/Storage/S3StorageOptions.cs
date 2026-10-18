namespace GDriveLight.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public const string SectionName = "Storage:S3";

    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
}
