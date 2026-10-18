using GDriveLight.Application.Files;

namespace GDriveLight.Application.Abstractions.Services;

public interface IFileContentPreparer
{
    // Returns rewindable content, its hash and actual size; rejects content exceeding the file limit.
    // Disposing the result releases owned resources and leaves the caller's source stream open.
    Task<PreparedFileContent> PrepareAsync(Stream source, CancellationToken cancellationToken = default);
}
