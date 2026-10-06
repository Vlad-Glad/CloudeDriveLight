using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public record GetFolderByIdQuery(Guid Id, Guid UserId) : IRequest<Result<FolderQuery>>;
