using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public record DeleteFolderCommand(
    Guid FolderId,
    Guid OwnerId) : IRequest<Result>;
