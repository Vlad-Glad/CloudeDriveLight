using GDriveLight.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;

namespace GDriveLight.Api.Configuration;

public static class HttpConfiguration
{
    private const long MultipartOverheadBytes = 1024 * 1024;

    public static void ConfigureHttp(this WebApplicationBuilder builder)
    {
        var uploadOptions = builder.Configuration.GetSection(FileContentPreparationOptions.SectionName)
            .Get<FileContentPreparationOptions>() ?? new FileContentPreparationOptions();
        var maxFileSize = uploadOptions.MaxFileSizeBytes;
        if (maxFileSize <= 0 || maxFileSize > long.MaxValue - MultipartOverheadBytes)
            throw new InvalidOperationException("Storage:MaxFileSizeBytes must be positive and leave room for multipart overhead.");

        var minimumBodySize = maxFileSize + MultipartOverheadBytes;
        var maxBodySize = builder.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize") ?? minimumBodySize;
        if (maxBodySize < minimumBodySize)
            throw new InvalidOperationException("Kestrel:Limits:MaxRequestBodySize must allow Storage:MaxFileSizeBytes plus 1 MiB of multipart overhead.");

        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxBodySize);
        // Transport buffering has its own ceiling. The preparer checks the actual file size before persistence.
        builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxBodySize);

        var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin)).ToArray();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
    }
}
