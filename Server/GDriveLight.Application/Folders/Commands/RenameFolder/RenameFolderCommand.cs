using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public record RenameFolderCommand(
    Guid FolderId,
    string NewName,
    Guid OwnerId) : IRequest<Result>;
