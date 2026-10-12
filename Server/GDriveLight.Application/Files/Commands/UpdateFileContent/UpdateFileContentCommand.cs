using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record UpdateFileContentCommand(
    Guid FileId,
    Stream FileStream,
    Guid UserId,
    Guid? OriginDeviceId = null) : IRequest<Result>;
