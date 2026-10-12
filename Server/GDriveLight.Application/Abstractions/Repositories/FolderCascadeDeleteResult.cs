using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public class FolderCascadeDeleteResult
{
    public IReadOnlyList<DriveFolder> DeletedFolders { get; }
    public IReadOnlyList<DriveFile> DeletedFiles { get; }
    public IReadOnlyList<string> FileUrls { get; }

    public FolderCascadeDeleteResult(
        IReadOnlyList<DriveFolder> deletedFolders,
        IReadOnlyList<DriveFile> deletedFiles,
        IReadOnlyList<string> fileUrls)
    {
        DeletedFolders = deletedFolders ?? throw new ArgumentNullException(nameof(deletedFolders));
        DeletedFiles = deletedFiles ?? throw new ArgumentNullException(nameof(deletedFiles));
        FileUrls = fileUrls ?? throw new ArgumentNullException(nameof(fileUrls));
    }
}
