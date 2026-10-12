using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public record CreateFolderCommand(
    string Name,
    Guid OwnerId,
    Guid? ParentFolderId = null,
    Guid? OriginDeviceId = null) : IRequest<Result<Guid>>;
