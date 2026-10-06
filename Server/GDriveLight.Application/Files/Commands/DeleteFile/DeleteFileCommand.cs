using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record DeleteFileCommand(Guid FileId, Guid UserId) : IRequest<Result>;
