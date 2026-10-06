using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record RenameFileCommand(Guid FileId, string NewName, Guid UserId) : IRequest<Result>;
