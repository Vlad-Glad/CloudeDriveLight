using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record MoveFileCommand(Guid FileId, Guid? NewFolderId, Guid UserId) : IRequest<Result>;
