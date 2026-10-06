using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public record GetFolderContentQuery(Guid? ParentFolderId, Guid UserId) : IRequest<Result<IEnumerable<FolderQuery>>>;
