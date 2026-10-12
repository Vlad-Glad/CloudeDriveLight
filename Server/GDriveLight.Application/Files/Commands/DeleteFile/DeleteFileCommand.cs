using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record DeleteFileCommand(Guid FileId, Guid UserId, Guid? OriginDeviceId = null) : IRequest<Result>;
