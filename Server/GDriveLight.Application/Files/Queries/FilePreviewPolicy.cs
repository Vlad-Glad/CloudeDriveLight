namespace GDriveLight.Application.Files.Queries;

public static class FilePreviewPolicy
{
    public static string NormalizeMimeType(string mimeType) =>
        mimeType.Split(';', 2)[0].Trim().ToLowerInvariant();

    public static string GetKind(string mimeType)
    {
        var mime = NormalizeMimeType(mimeType);
        if (mime.StartsWith("text/", StringComparison.Ordinal) ||
            mime is "application/json" or "application/xml" or "application/javascript" or "application/x-javascript" ||
            mime.EndsWith("+json", StringComparison.Ordinal) || mime.EndsWith("+xml", StringComparison.Ordinal) && mime != "image/svg+xml")
            return "text";

        return mime switch
        {
            "image/jpeg" or "image/png" or "image/gif" or "image/webp" or "image/avif" or "image/bmp" => "image",
            "application/pdf" => "pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "docx",
            _ => "none"
        };
    }

    // Active documents and source code are always served as inert text.
    public static string GetContentType(string mimeType) =>
        GetKind(mimeType) == "text" ? "text/plain" : NormalizeMimeType(mimeType);
}
