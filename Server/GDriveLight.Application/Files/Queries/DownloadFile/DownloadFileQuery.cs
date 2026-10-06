using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public record DownloadFileResult(Stream FileStream, string FileName, string MimeType);

public record DownloadFileQuery(Guid FileId, Guid UserId) : IRequest<Result<DownloadFileResult>>;
