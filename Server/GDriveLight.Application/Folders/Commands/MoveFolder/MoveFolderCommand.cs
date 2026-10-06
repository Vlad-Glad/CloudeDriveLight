using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public record MoveFolderCommand(Guid FolderId, Guid? NewParentFolderId, Guid UserId) : IRequest<Result>;
