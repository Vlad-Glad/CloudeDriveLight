using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure.Services;
using GDriveLight.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace GDriveLight.Tests;

internal static class TestFileContentPreparer
{
    public static IFileContentPreparer Create(long? maxFileSizeBytes = null)
    {
        var options = new FileContentPreparationOptions();
        if (maxFileSizeBytes.HasValue) options.MaxFileSizeBytes = maxFileSizeBytes.Value;
        return new FileContentPreparer(Options.Create(options));
    }
}
