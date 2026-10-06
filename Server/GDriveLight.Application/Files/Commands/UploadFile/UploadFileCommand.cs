using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public record UploadFileCommand(
    string FileName,
    Stream FileStream,
    Guid UserId,
    Guid? FolderId) : IRequest<Result<Guid>>;
