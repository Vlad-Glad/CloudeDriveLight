using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public record GetFolderPathQuery(Guid FolderId, Guid UserId) : IRequest<Result<IEnumerable<FolderQuery>>>;
