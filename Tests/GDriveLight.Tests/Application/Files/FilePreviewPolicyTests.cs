using GDriveLight.Application.Files.Queries;

namespace GDriveLight.Tests.Application.Files;

public sealed class FilePreviewPolicyTests
{
    [Theory]
    [InlineData("text/x-csharp", "text", "text/plain")]
    [InlineData("text/html", "text", "text/plain")]
    [InlineData("application/javascript", "text", "text/plain")]
    [InlineData("application/vnd.custom+json", "text", "text/plain")]
    [InlineData("Text/Markdown; charset=utf-8", "text", "text/plain")]
    [InlineData("image/jpeg", "image", "image/jpeg")]
    [InlineData("image/png", "image", "image/png")]
    [InlineData("application/pdf", "pdf", "application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("image/svg+xml", "none", "image/svg+xml")]
    [InlineData("application/octet-stream", "none", "application/octet-stream")]
    [InlineData("application/zip", "none", "application/zip")]
    public void ClassifiesMimeTypesWithoutDependingOnFileExtensions(string mime, string kind, string contentType)
    {
        Assert.Equal(kind, FilePreviewPolicy.GetKind(mime));
        Assert.Equal(contentType, FilePreviewPolicy.GetContentType(mime));
    }
}
