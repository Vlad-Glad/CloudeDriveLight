using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands.CreateFolder;

public record CreateFolderCommand(
    string Name,
    Guid OwnerId,
    Guid? ParentFolderId = null) : IRequest<Result<Guid>>;
