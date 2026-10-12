using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record UploadFileCommand(
    string FileName,
    Stream FileStream,
    Guid UserId,
    Guid? FolderId,
    Guid? OriginDeviceId = null) : IRequest<Result<Guid>>;

