using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public record GetFileByIdQuery(Guid FileId, Guid UserId) : IRequest<Result<FileQuery>>;
